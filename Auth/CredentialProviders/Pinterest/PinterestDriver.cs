using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Credentials
{
    public class PinterestDriver
    {
        private string accessToken;

        public PinterestDriver(string accessToken)
        {
            this.accessToken = accessToken;
        }

        private async Task<TResult> GetAsync<TResource, TResult>(string path,
                string fields,
            Func<TResource, TResult> onFound,
            Func<string, TResult> onFailure)
        {
            var baseUrl = new Uri($"https://api.pinterest.com/v1/{path}?access_token={accessToken}");
            if (fields.HasBlackSpace())
                baseUrl = baseUrl.AddQueryParameter("fields", fields);
            var userRequest = new HttpRequestMessage(
                new HttpMethod("GET"),
                baseUrl);
            //userRequest.Headers.Authorization = new AuthenticationHeaderValue(tokenType, accessToken);
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.SendAsync(userRequest);
                var content = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return onFailure(content);

                try
                {
                    var resource = JsonConvert.DeserializeObject<Data<TResource>>(content);
                    return onFound(resource.data);
                }
                catch (Newtonsoft.Json.JsonReaderException)
                {
                    return onFailure($"Pinterest returned non-json response:{content}");
                }
            }
        }

        [DataContract]
        public struct Image
        {
            [JsonProperty]
            public ImageLink original;
        }

        [DataContract]
        public struct ImageLink
        {
            [JsonProperty]
            public int width;

            [JsonProperty]
            public int height;

            /// <summary>
            /// The link to the image.
            /// </summary>
            [JsonProperty]
            public string url;
        }

        #region User

        public struct User
        {
            public const string IdPropetyName = "id";
            [JsonProperty(PropertyName = IdPropetyName)]
            public string id;

            public const string FirstNamePropertyName = "first_name";
            [JsonProperty(PropertyName = FirstNamePropertyName)]
            public string firstName;

            public const string LastNamePropertyName = "last_name";
            [JsonProperty(PropertyName = LastNamePropertyName)]
            public string lastName;

            public const string UrlPropertyName = "url";
            [JsonProperty(PropertyName = UrlPropertyName)]
            public string url;
        }

        private struct Data<TResource>
        {
            [JsonProperty(PropertyName = "data")]
            public TResource data;
        }

        public Task<TResult> UserAsync<TResult>(
            Func<User, TResult> onFound,
            Func<TResult> onNotFound = default,
            Func<string, TResult> onFailure = default)
        {
            return UserAsync("me",
                onFound,
                onNotFound,
                onFailure);
        }

        public Task<TResult> UserAsync<TResult>(string userId,
            Func<User, TResult> onFound,
            Func<TResult> onNotFound = default,
            Func<string, TResult> onFailure = default)
        {
            return GetAsync<User, TResult>(userId,
                    default,
                (user) => onFound(user),
                onFailure);
        }

        #endregion

        #region Boards

        public struct Board
        {
            /// <summary>
            /// The unique string of numbers and letters that identifies the board on Pinterest.
            /// </summary>
            [JsonProperty]
            public string id;

            /// <summary>
            /// The name of the board.
            /// </summary>
            [JsonProperty]
            public string name;

            /// <summary>
            /// The link to the board.
            /// </summary>
            [JsonProperty]
            public string url;

            /// <summary>
            /// The user-entered description of the board.
            /// </summary>
            [JsonProperty]
            public string description;

            /// <summary>
            /// The first and last name, ID and profile URL of the user who created the board.
            /// </summary>
            [JsonProperty]
            public User creator;

            /// <summary>
            /// ISO 8601 format The date the user created the board.
            /// </summary>
            [JsonProperty(PropertyName = "created_at")]
            public DateTime createdAt;

            /// <summary>
            /// The board’s stats, including how many Pins, followers, user's following and collaborators it has.
            /// </summary>
            [JsonProperty]
            public IDictionary<string, int> counts;

            /// <summary>
            /// The user’s profile image. The response returns the image’s URL, width and height.
            /// </summary>
            [JsonProperty]
            public IDictionary<string, ImageLink> image;
        }

        public Task<TResult> BoardsAsync<TResult>(
            Func<Board [], TResult> onFound,
            Func<string, TResult> onFailure)
        {
            return GetAsync<Board[], TResult>("me/boards",
                    "url,creator,created_at,description,counts,image",
                (boards) => onFound(boards),
                onFailure);
        }

        #endregion

        public struct Pin
        {
            /// <summary>
            /// The unique string of numbers and letters that identifies the Pin on Pinterest.
            /// </summary>
            [JsonProperty]
            public string id;

            /// <summary>
            /// The URL of the webpage where the Pin was created.
            /// </summary>
            [JsonProperty]
            public string link;

            /// <summary>
            /// The board that the Pin is on.
            /// </summary>
            [JsonProperty]
            public Board board;

            /// <summary>
            /// The URL of the Pin on Pinterest.
            /// </summary>
            [JsonProperty]
            public string url;

            /// <summary>
            /// The user-entered description of the Pin.
            /// </summary>
            [JsonProperty]
            public string note;

            /// <summary>
            /// The first and last name, ID and profile URL of the user who created the board.
            /// </summary>
            [JsonProperty]
            public User creator;

            /// <summary>
            /// ISO 8601 format The date the Pin was created.
            /// </summary>
            [JsonProperty(PropertyName = "created_at")]
            public DateTime createdAt;

            /// <summary>
            /// The Pin’s stats, including the number of repins, comments.
            /// </summary>
            [JsonProperty]
            public IDictionary<string, int> counts;

            /// <summary>
            /// The Pin’s image. The default response returns the image’s URL, width and height.
            /// </summary>
            [JsonProperty]
            public Image? image;

            /// <summary>
            /// The dominant color of the Pin’s image in hex code format.
            /// </summary>
            [JsonProperty]
            public string color;

            /// <summary>
            /// The media type of the Pin (image or video).
            /// </summary>
            [JsonProperty]
            public IDictionary<string, string> media;

            /// <summary>
            /// The source data for videos, including the title, URL, provider, author name, author URL and provider name.
            /// </summary>
            [JsonProperty]
            public IDictionary<string, string> attribution;

            /// <summary>
            /// Extra information about the Pin for Rich Pins. Includes the Pin type (e.g., article, recipe) and related information (e.g., ingredients, author).
            /// </summary>
            [JsonProperty]
            public IDictionary<string, object> metadata;
        }

        public Task<TResult> PinsAsync<TResult>(string boardId,
            Func<Pin[], TResult> onFound,
            Func<string, TResult> onFailure)
        {
            return GetAsync<Pin[], TResult>($"boards/{boardId}/pins",
                "created_at,link,creator,note,color,counts,media,attribution,image", //,metadata",
                (boards) => onFound(boards),
                onFailure);
        }

        public Task<TResult> PinAsync<TResult>(string pinId,
            Func<Pin, TResult> onFound,
            Func<string,TResult> onFailure)
        {
            return GetAsync<Pin, TResult>($"pins/{pinId}",
                    "created_at,link,creator,board,note,color,counts,media,attribution,image,metadata",
                (pin) => onFound(pin),
                onFailure);
        }
    }
}
