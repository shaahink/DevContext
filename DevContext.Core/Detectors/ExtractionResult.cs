namespace DevContext.Core.Extractors
{
    public class ExtractionResult
    {
        public ExtractionResult(string id, string content)
        {
            Id = id;
            Content = content;
        }

        public string Id { get; }
        public string Content { get; }
    }
}
