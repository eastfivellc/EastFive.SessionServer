using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using BlackBarLabs.Web;
using BlackBarLabs.Api;
using System.Threading.Tasks;

namespace EastFive.Api.Azure.Controllers
{
    [FunctionViewController(Route = "Content")]
    public static class ContentController
    {
        [HttpPost]
        public static async Task<HttpResponseMessage> CreateContentAsync(
            [RequiredAndAvailableInPathAttribute]Guid contentId, [Required]ByteArrayContent content,
            HttpRequestMessage request)
        {
            var contentType = content.Headers.ContentType.MediaType;
            var contentBytes = await content.ReadAsByteArrayAsync();
            return await Content.CreateContentAsync(contentId, contentType, contentBytes,
                () => request.CreateResponse(HttpStatusCode.Created),
                () => request.CreateResponse(HttpStatusCode.Conflict).AddReason($"Content with name {contentId} already exists"));
        }

        [HttpGet]
        public static async Task<HttpResponseMessage> QueryByContentIdAsync([RequiredAndAvailableInPathAttribute]Guid contentId,
                [Optional]int? width, [Optional]int? height, [Optional]bool? fill,
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

        private static async Task<HttpResponseMessage> QueryAsVideoStream([Required]Guid contentId, [Required]bool streaming, HttpRequestMessage request, System.Web.Http.Routing.UrlHelper url)
        {
            var response = await Content.FindContentByContentIdAsync(contentId, new System.Security.Claims.Claim[] { },
                (contentType, video) => request.CreateResponseVideoStream(video, contentType),
                () => request.CreateResponse(HttpStatusCode.NotFound),
                () => request.CreateResponse(HttpStatusCode.Unauthorized));
            return response;
        }
    }
}
