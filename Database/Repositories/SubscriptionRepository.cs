﻿using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Database.Repositories;

public class SubscriptionRepository(WebPushAppContext _db, ILogger<SubscriptionRepository> _logger) : ISubscriptionRepository
{
    public async Task<List<Subscription>> GetAllNonSenderSubscriptionsAsync(string senderId)
    {
        return await _db.Subscriptions.Where(s => s.UserId != senderId).ToListAsync();
    }

    public async Task<List<Subscription>> GetUserSubscriptionsAsync(string subscriberId)
    {
        List<Subscription> subscriptions = await _db.Subscriptions.Where(s => s.UserId == subscriberId).ToListAsync();
        return subscriptions;
    }

    public async Task<bool> IsUserSubscriptionAsync(string subscriptionString, string userId)
    {
        try
        {
            AplicationUser? user = await _db.Users.Include(u => u.Subscriptions).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogError("User with ID {userId} not found in database.", userId);
                return false;
            }

            _logger.LogInformation("User subscriptions in DB: {subscriptions}", user.Subscriptions.Count);

            bool subscriptionExists = user.Subscriptions.Any(s => s.SubscriptionJson == subscriptionString);
            _logger.LogInformation("Subscription match found: {subscriptionExists}", subscriptionExists);

            return subscriptionExists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while checking subscription for user {userId}.", userId);
            return false;
        }
    }


    public async Task<bool> RemoveSubscriptionAsync(string subscriptionString)
    {
        Subscription? subscription = _db.Subscriptions.FirstOrDefault(s => s.SubscriptionJson.Equals(subscriptionString));
        int success = 0;
        if (subscription is not null)
        {
            subscription.IsDeleted = true;
            success = await _db.SaveChangesAsync();
        }
        if (subscription != null && success != 0)
        {
            _logger.LogInformation("subscription successfully removed from database.");
            return true;
        }
        else
        {
            _logger.LogError("Subscription not removed from database.");
            return false;
        }
    }

    public async Task<int> SaveSubscriptionAsync(string subscriptionString, string userId)
    {
        AplicationUser? user = await _db.Users.FindAsync(userId);
        if (user == null)
        {
            _logger.LogError("user with id = {userId} was not found in database", userId);
            return 0;
        }
        List<Subscription> subscriptions = user.Subscriptions.ToList();
        foreach(Subscription sub in subscriptions)
        {
            if (sub.SubscriptionJson.Equals(subscriptionString))
            {
                _logger.LogError("Subscription already exists in the database with id = {SubscriptionId}", sub.Id);
                return -1; //representing no change, but existing subscription.
            }
        }



        Subscription subscription = new() { SubscriptionJson = subscriptionString };
        user.Subscriptions.Add(subscription);
        int success = await _db.SaveChangesAsync();
        if (success == 0)
        {
            _logger.LogError("Failed to save subscription to database.");
            return 0;
        }
        return subscription.Id;
    }
}