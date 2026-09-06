namespace PursuitHQ.API.Models
{
    /// <summary>One card in a deck.</summary>
    public class Flashcard
    {
        public int Id { get; set; }

        public int DeckId { get; set; }
        public FlashcardDeck? Deck { get; set; }

        /// <summary>Question or term.</summary>
        public string Front { get; set; } = string.Empty;

        /// <summary>Answer or definition.</summary>
        public string Back { get; set; } = string.Empty;

        public int Order { get; set; }

        /// <summary>Review counters, so a student can drill the cards they keep missing.</summary>
        public int TimesReviewed { get; set; }
        public int TimesCorrect { get; set; }
    }
}
