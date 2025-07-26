namespace DiscordBot
{
    public class Suggestion
    {
        public int Id { get; set; }
        public ulong AuthorId { get; set; }
        public required string Content { get; set; }
        public DateTime Timestamp { get; set; }
        public required HashSet<ulong> LikedBy { get; set; }
        public required HashSet<ulong> DislikedBy { get; set; }
        public ulong? ChannelId { get; set; }
        public ulong? MessageId { get; set; }

        public int Likes => LikedBy.Count;
        public int Dislikes => DislikedBy.Count;
    }
}
