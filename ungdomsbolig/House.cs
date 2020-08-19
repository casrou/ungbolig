namespace ungdomsbolig
{
    public class House : ILivable
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Size { get; set; }
        public string WaitingPeriod { get; set; }
        public int Quantity { get; set; }
        public int Type { get; set; }
        public decimal Rent { get; set; }
        public decimal DownPayment { get; set; }
        public string Url { get; set; }
        public string FloorPlanUrl { get; set; }

        public override bool Equals(object obj)
        {
            //return base.Equals(obj);
            return ((House)obj).Url == this.Url;
        }
    }

    //public static class NumberOfRooms
    //{
    //    public const string ROOM = "Værelse";
    //    public const string ONE = "1 Værelses lejlighed";
    //    public const string ONEPLUS = "1½ Værelses lejlighed";
    //    public const string TWOSHARE = "2 Værelses lejlighed (delevenlig)";
    //    public const string TWO = "2 Værelses lejlighed";
    //    public const string TWOPLUSTHREE = "2½ - 3 Værelses lejlighed";

    //    public string Choose(int choice)
    //    {
    //        switch (choice)
    //        {
    //            case choice <= 0:
    //                return NumberOfRooms.ROOM;
    //            case choice
    //            default:
    //                break;
    //        }
    //    }
    //};
}
