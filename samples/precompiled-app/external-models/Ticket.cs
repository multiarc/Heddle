namespace Acme.Models
{
    /// <summary>A model type that lives outside the app's compile-time reference set.</summary>
    public sealed class Ticket
    {
        public string Reference { get; set; }

        public string Passenger { get; set; }

        public string Seat { get; set; }
    }
}
