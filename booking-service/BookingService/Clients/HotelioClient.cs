using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace BookingService.Clients;

public class HotelioClient
{
    private readonly HttpClient _httpClient;

    public HotelioClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
    
    public async Task<bool> IsActiveUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var path = $"users/{userId}/active";
        return await GetBooleanResponseAsync(
            path, 
            cancellationToken);
    }
    
    public async Task<bool> IsUserInBlackListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var path = $"users/{userId}/blacklisted";
        return await GetBooleanResponseAsync(
            path, 
            cancellationToken);
    }
    
    public async Task<string?> GetUserStatusAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var path = $"users/{userId}/status";
        var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
    
    public async Task<bool> IsTrustedHotelAsync(
        string hotelId,
        CancellationToken cancellationToken = default)
    {
        var path = $"reviews/hotel/{hotelId}/trusted";
        return await GetBooleanResponseAsync(
            path, 
            cancellationToken);
    }
    
    public async Task<bool> IsOperationalHotelAsync(
        string hotelId,
        CancellationToken cancellationToken = default)
    {
        var path = $"hotels/{hotelId}/operational";
        return await GetBooleanResponseAsync(
            path,
            cancellationToken);
    }
    
    public async Task<bool> IsFullyBookedHotelAsync(
        string hotelId,
        CancellationToken cancellationToken = default)
    {
        var path = $"hotels/{hotelId}/fully-booked";
        return await GetBooleanResponseAsync(
            path,
            cancellationToken);
    }

    public async Task<decimal?> GetPromoDiscountAsync(
        string? promoCode,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        const string path = "promos/validate";
        var queryParams = new Dictionary<string, string?>();
        if (!string.IsNullOrEmpty(promoCode))
        {
            queryParams.Add("code", promoCode);
        }
        if (!string.IsNullOrEmpty(userId))
        {
            queryParams.Add("userId", userId);
        }
        var requestUri = QueryHelpers.AddQueryString(
            path,
            queryParams);
        var response = await _httpClient.PostAsync(requestUri, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (json == string.Empty)
        {
            return null;
        }
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("discount")
            .GetDecimal();
    }

    #region private
    
    private async Task<bool> GetBooleanResponseAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        return bool.Parse(result);
    }
    
    #endregion
}