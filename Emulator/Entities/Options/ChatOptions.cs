namespace Emulator.Entities.Options;

public class ChatOptions
{
    public string ServiceId { get; set; }
    public IEnumerable<string> DefaultRooms { get; set; }
    public IEnumerable<string> Administrators { get; set; }
    public string RoomServiceFormat { get; set; }
    public string ClanServiceFormat { get; set; }
}