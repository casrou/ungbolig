using System.Linq;

namespace ungdomsbolig
{
    public class House : ILivable
    {
        public int Id { get; set; }
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
        public string Address { get => Description.Substring(Description.IndexOf('-') + 2); }

        public override bool Equals(object obj)
        {
            var house = obj as House;
            if (house == null) return false;
            return ToString() == house.ToString();
                //base.Equals(obj);
        }

        public override string ToString()
        {
            return Url + "-" + Type + "-" + Size + "-" + Quantity + "-" + DownPayment + "-" + Rent;
        }

        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

    }
    
}
