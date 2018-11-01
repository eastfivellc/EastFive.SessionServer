using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using BlackBarLabs.Web;
using BlackBarLabs.Api;
using System.Threading.Tasks;
using EastFive.Api.Controllers;

namespace EastFive.Api.Azure.Controllers
{
    [FunctionViewController(Route = "Content")]
    public static class ContentController
    {
        [HttpPost]
        public static async Task<HttpResponseMessage> CreateContentAsync(
                [QueryParameter(CheckFileName = true)]Guid contentId,
                [QueryParameter]ByteArrayContent content,
                HttpRequestMessage request,
            CreatedResponse onCreated,
            AlreadyExistsResponse onAlreadyExists)
        {
            var contentType = content.Headers.ContentType.MediaType;
            var contentBytes = await content.ReadAsByteArrayAsync();
            return await Content.CreateContentAsync(contentId, contentType, contentBytes,
                () => onCreated(),
                () => onAlreadyExists());
        }

        [HttpGet]
        public static async Task<HttpResponseMessage> QueryByContentIdAsync(
                [QueryParameter(CheckFileName = true)]Guid contentId,
                [OptionalQueryParameter]int? width,
                [OptionalQueryParameter]int? height,
                [OptionalQueryParameter]bool? fill,
            HttpRequestMessage request, System.Web.Http.Routing.UrlHelper url)
        {
            var response = await Content.FindContentByContentIdAsync(contentId, new System.Security.Claims.Claim[] { },
                (contentType, image) =>
                {
                    if (contentType.StartsWith("video", StringComparison.InvariantCultureIgnoreCase) &&
                        (width.HasValue || height.HasValue || fill.HasValue))
                    {
                        var videoPreviewImage = default(System.Drawing.Image); // Properties.Resources.video_preview;
                        return request.CreateImageResponse(videoPreviewImage,
                            width: width, height: height, fill: fill,
                            filename: contentId.ToString("N"));
                    }
                    return request.CreateImageResponse(image,
                        width: width, height: height, fill: fill,
                        filename: contentId.ToString("N"),
                        contentType: contentType);
                },
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
            return response;
        }

        private static async Task<HttpResponseMessage> QueryAsVideoStream(
            [QueryParameter]Guid contentId,
            [QueryParameter]bool streaming, 
            HttpRequestMessage request, 
            System.Web.Http.Routing.UrlHelper url)
        {
            var response = await Content.FindContentByContentIdAsync(contentId, new System.Security.Claims.Claim[] { },
                (contentType, video) => request.CreateResponseVideoStream(video, contentType),
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
            return response;
        }
    }
}
