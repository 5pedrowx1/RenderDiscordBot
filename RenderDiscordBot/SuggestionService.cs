namespace DiscordBot
{
    public class SuggestionService
    {
        private readonly List<Suggestion> _suggestions = [];
        private int _nextId = 1;

        public Suggestion AddSuggestion(ulong authorId, string content)
        {
            var suggestion = new Suggestion
            {
                Id = _nextId++,
                AuthorId = authorId,
                Content = content,
                Timestamp = DateTime.UtcNow,
                LikedBy = [],
                DislikedBy = []
            };
            _suggestions.Add(suggestion);
            return suggestion;
        }

        public Suggestion? GetSuggestion(int id)
            => _suggestions.FirstOrDefault(s => s.Id == id);
    }
}
