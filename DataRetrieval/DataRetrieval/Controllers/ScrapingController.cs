﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RestSharp;
using ScrapySharp.Extensions;
using ScrapySharp.Html.Dom;

namespace DataRetrieval.Controllers
{
    public class ScrapingController : Controller
    {
        private readonly IRestClient imdbClient;

        public ScrapingController(IRestClient imdbClient)
        {
            this.imdbClient = imdbClient;
        }

//        public async Task<IEnumerable<IActionResult>> GetFilmsInfoFromImdb(IEnumerable<int> ids, int count = 10)
//        {
//            var results = new List<{}>();
//            foreach (var id in ids.Take(count))
//                await GetSingleFilmInfoFromImdb(id.ToString()).ConfigureAwait(false);
//        }


        public async Task<IActionResult> GetSingleFilmInfoFromImdb(string filmId = "4743226")
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

            var data = new {rating, name, year, date, genres, direc = director, stars, storyLine, synopsis};
//            return data;
            return Json(data);
        }

        private static (string name, string year) ExtractNameAndYear(HDocument document)
        {
            var nameAndYear = document.CssSelect(".title_wrapper>h1").Single();
            return (nameAndYear.Children[0].InnerText.Replace("&nbsp", " ").Trim(), nameAndYear.Children[1].InnerText);
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

        private static string ExtractDate(HDocument document)
        {
            try
            {
                var dateTitle = document.CssSelect("#titleDetails div.txt-block h4")
                    .Single(e => e.InnerText == "Release Date:");
                var date = dateTitle.ParentNode.Children[2].InnerText;
                date = date.Substring(0, date.LastIndexOf("(", StringComparison.Ordinal)).Trim();
                return date;
            }
            catch
            {
                return "";
            }
        }

        private static string ExtractRating(HDocument document)
        {
            try
            {
                return document.CssSelect(".ratings_wrapper .imdbRating .ratingValue strong span").Single().InnerText;
            }
            catch (Exception)
            {
                return "";
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