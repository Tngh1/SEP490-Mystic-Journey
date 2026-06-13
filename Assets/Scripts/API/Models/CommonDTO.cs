namespace MysticJourney.API.Models.Response
{
    // Wrapper chung cho pagination 
    // Dùng cho: DailyLoginRewardsController (PagedResultDto<T>)
    [System.Serializable]
    public class PagedResultResponse<T>
    {
        public int TotalCount { get; set; }
        public T[] Items { get; set; }
    }

    // Dùng cho: DungeonsController, QuestsController, AchievementsController,
    //           GachaBannersController, ShopItemsController (PaginatedResponseDto<T>)
    [System.Serializable]
    public class PaginatedResponse<T>
    {
        public T[] Items { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }
}
