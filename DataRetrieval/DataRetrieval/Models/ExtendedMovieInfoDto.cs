using System;

namespace DataRetrieval.Models
{
    public class ExtendedMovieInfoDto
    {
        public string Id { get; set; }
        public int? Year { get; set; }
        public string Name { get; set; }
        public float? Rating { get; set; }
        public DateTime? PremiereDate { get; set; }
        public string[] Genres { get; set; }
        public string Director { get; set; }
        public string[] Stars { get; set; }
        public string StoryLine { get; set; }
        public string Synopsis { get; set; }
    }
}
