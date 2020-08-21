namespace ungdomsbolig
{
    public interface ILivable
    {
        string Name { get; set; }
        string Description { get; set; }
        string Address { get; }
        string Url { get; set; }
        decimal Rent { get; set; }
        decimal DownPayment { get; set; }
        decimal Size { get; set; }
        string WaitingPeriod { get; set; }
        int Quantity { get; set; }
        int Type { get; set; }
        string FloorPlanUrl { get; set; }
    }
}
