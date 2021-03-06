﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DataRetrieval.DbProvider;
using DataRetrieval.Models;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using RestSharp;
using ScrapySharp.Extensions;
using ScrapySharp.Html.Dom;

namespace DataRetrieval.Controllers
{
    public class ScrapingController : Controller
    {
        private readonly PostgreSqlDbProvider dbProvider;
        private readonly IRestClient imdbClient;

        public ScrapingController(IRestClient imdbClient, PostgreSqlDbProvider dbProvider)
        {
            this.imdbClient = imdbClient;
            this.dbProvider = dbProvider;
        }

        public async Task<IEnumerable<ExtendedMovieInfoDto>> GetFilmsInfoFromImdb(int count = 5, bool countAll = false)
        {
            if (countAll)
                count = int.MaxValue;

            var movies = await dbProvider.GetRowsAsync("movies", count: count).ConfigureAwait(false);

            var extendedMovies = new List<ExtendedMovieInfoDto>();
            foreach (var movie in movies)
            {
                var extendedMovieInfoDto = await GetSingleFilmInfoFromImdb(formatImdbId(movie["id"].ToString()))
                    .ConfigureAwait(false);
                if (extendedMovieInfoDto != null)
                    extendedMovies.Add(extendedMovieInfoDto);
            }

            return extendedMovies;
        }

        public async Task<IActionResult> FillExtendedMoviesTable(int count = 5, bool countAll = false)
        {
            if (countAll)
                count = int.MaxValue;

            var getInfoSw = new Stopwatch();
            getInfoSw.Start();
            var extendedMovies = await GetFilmsInfoFromImdb(count, countAll).ConfigureAwait(false);
            getInfoSw.Stop();


            var writeDataSw = new Stopwatch();
            writeDataSw.Start();

            var itemsAdded = 0;

            //todo: вынести в конфиг
            var connectionString =
                "Host=db.mirvoda.com;Port=5454;Database=CoderLiQ;Username=developer;Password=rtfP@ssw0rd";

            using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync().ConfigureAwait(false);

                foreach (var movie in extendedMovies)
                {
                    try
                    {
                        var command =
                            "INSERT INTO extended_movies (id, name, premiere_date, genres, director, stars, storyline, synopsis, rating) " +
                            "VALUES (@id, @name, @premiere_date, @genres, @director, @stars, @storyline, @synopsis, @rating)";

                        using (var cmd = new NpgsqlCommand(command, conn))
                        {
                            cmd.Parameters.AddWithValue("id", int.Parse(movie.Id));
                            cmd.Parameters.AddWithValue("name", movie.Name);
                            cmd.Parameters.AddWithValue("premiere_date", movie.PremiereDate);
                            cmd.Parameters.AddWithValue("genres", movie.Genres);
                            cmd.Parameters.AddWithValue("director", movie.Director);
                            cmd.Parameters.AddWithValue("stars", movie.Stars);
                            cmd.Parameters.AddWithValue("storyline", movie.StoryLine);
                            cmd.Parameters.AddWithValue("synopsis", movie.Synopsis);
                            cmd.Parameters.AddWithValue("rating", movie.Rating);

                            await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
                            itemsAdded++;
                        }
                    }
                    catch
                    {
//                     ignored
                    }
                }
            }

            writeDataSw.Stop();
            return Json(new { GetDataInSeconds = getInfoSw.Elapsed.Seconds, WriteDataInSeconds = writeDataSw.Elapsed.Seconds, itemsAdded});
        }

        private string formatImdbId(string Id)
        {
            var idRequiredLength = 7;

            if (Id.Length < idRequiredLength)
            {
                var missing = idRequiredLength - Id.Length;

                return Id.Insert(0, new string('0', missing));
            }

            return Id;
        }

        public async Task<ExtendedMovieInfoDto> GetSingleFilmInfoFromImdb(string filmId = "4743226")
        {
            try
            {
                var document = HDocument.Parse(
                    (await imdbClient
                        .ExecuteTaskAsync(new RestRequest($"title/tt{filmId}/")).ConfigureAwait(false))
                    .Content);

                var (name, year) = ExtractNameAndYear(document);
                var rating = ExtractRating(document);
                var date = ExtractDate(document);
                var genres = ExtractGenres(document);
                var director = ExtractDirector(document);
                var stars = ExtractStars(document);
                var storyLine = ExtractStoryLine(document);
                var synopsis = await ExtractSynopsis(document, imdbClient).ConfigureAwait(false);

                return new ExtendedMovieInfoDto
                {
                    Id = filmId,
                    Name = name,
                    Year = year,
                    Rating = rating,
                    PremiereDate = date,
                    Genres = genres.ToArray(),
                    Director = director,
                    Stars = stars.ToArray(),
                    StoryLine = storyLine,
                    Synopsis = synopsis
                };
            }
            catch (Exception e)
            {
                return null;
            }
        }

        private static (string name, int? year) ExtractNameAndYear(HDocument document)
        {
            var nameAndYear = document.CssSelect(".title_wrapper>h1").Single();
            int.TryParse(nameAndYear.Children[1].InnerText, out var year);

            var name = nameAndYear.Children[0].InnerText.Replace("&nbsp", " ").Trim();

            if (name == "" || year == default(int))
                throw new ArgumentException();

            return (name, year);
        }

        private static string ExtractStoryLine(HDocument document)
        {
            try
            {
                return document.CssSelect("#titleStoryLine div.inline.canwrap p span").Single().InnerText.Trim();
            }
            catch
            {
                return "";
            }
        }

        private static IEnumerable<string> ExtractStars(HDocument document)
        {
            try
            {
                var stars = document.CssSelect("div.plot_summary h4")
                    .Single(e => e.InnerText == "Stars:").ParentNode.CssSelect("a")
                    .Where(a => a.GetAttributeValue("href", "") != "fullcredits/?ref_=tt_ov_st_sm")
                    .Select(e => e.InnerText.Trim());
                return stars;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static string ExtractDirector(HDocument document)
        {
            try
            {
                var direc = document.CssSelect("div.plot_summary h4")
                    .Single(e => e.InnerText == "Director:").ParentNode.CssSelect("a").Single().InnerText;
                return direc;
            }
            catch
            {
                return "";
            }
        }

        private static IEnumerable<string> ExtractGenres(HDocument document)
        {
            try
            {
                var genres = document.CssSelect("#titleStoryLine div h4")
                    .Single(e => e.InnerText == "Genres:").ParentNode.CssSelect("a").Select(e => e.InnerText.Trim());
                return genres;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static DateTime? ExtractDate(HDocument document)
        {
            try
            {
                var dateTitle = document.CssSelect("#titleDetails div.txt-block h4")
                    .Single(e => e.InnerText == "Release Date:");
                var date = dateTitle.ParentNode.Children[2].InnerText;
                date = date.Substring(0, date.LastIndexOf("(", StringComparison.Ordinal)).Trim();
                return Convert.ToDateTime(date);
            }
            catch
            {
                return null;
            }
        }

        private static float? ExtractRating(HDocument document)
        {
            try
            {
                var readOnlySpan = document.CssSelect(".ratings_wrapper .imdbRating .ratingValue strong span").Single()
                    .InnerText;
                float.TryParse(readOnlySpan, NumberStyles.Any, CultureInfo.InvariantCulture.NumberFormat,
                    out var rating);
                return rating == 0 ? (float?) null : rating;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string> ExtractSynopsis(HDocument document, IRestClient imdbClient)
        {
            try
            {
                return HDocument.Parse((
                            await imdbClient.ExecuteTaskAsync(
                                new RestRequest(document.CssSelect("#titleStoryLine a")
                                    .Single(a => a.InnerText == "Plot Synopsis")
                                    .GetAttributeValue("href", ""))).ConfigureAwait(false))
                        .Content)
                    .CssSelect("#plot-synopsis-content li").Single().InnerText.Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}