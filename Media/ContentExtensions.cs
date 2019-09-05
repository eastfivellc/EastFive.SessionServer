﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EastFive.Api;
using EastFive.Api.Azure.Resources;
using EastFive.Azure.Persistence.AzureStorageTables;
using EastFive.Extensions;
using EastFive.Linq;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace EastFive.Azure.Media
{
    public static class ContentExtensions
    {
        public static Task<TResult> LoadBytesAsync<TResult>(this IRef<Content> contentRef,
            Func<byte [], string, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return contentRef.id.BlobLoadBytesAsync("content",
                onFound,
                onNotFound);
        }

        public static Task<TResult> LoadStreamAsync<TResult>(this IRef<Content> contentRef,
            Func<Stream, string, TResult> onFound,
            Func<TResult> onNotFound)
        {
            return contentRef.id.BlobLoadStreamAsync("content",
                onFound,
                onNotFound);
        }

        public static Task<TResult> SaveBytesAsync<TResult>(this IRef<Content> contentRef, 
                byte[] content,
            Func<TResult> onSuccess,
            Func<TResult> onAlreadyExists,
            Func<StorageTables.Driver.ExtendedErrorInformationCodes, string, TResult> onFailure = default,
            string contentType = default,
            Azure.StorageTables.Driver.AzureStorageDriver.RetryDelegate onTimeout = null)
        {
            return content.BlobCreateAsync(contentRef.id, "content",
                onSuccess,
                onAlreadyExists,
                onFailure: onFailure,
                contentType: contentType,
                onTimeout: onTimeout);
        }

        public static Task<ImageAnalysis> AnalyzeAsync(this IRef<Content> contentRef)
        {
            return Web.Configuration.Settings.GetString(
                    EastFive.Azure.AppSettings.CognitiveServices.ComputerVisionSubscriptionKey,
                subscriptionKey =>
                {
                    return Web.Configuration.Settings.GetUri(
                            EastFive.Azure.AppSettings.CognitiveServices.ComputerVisionEndpoint,
                        async endpointUri =>
                        {
                            using (var computerVision = new ComputerVisionClient(
                                new ApiKeyServiceClientCredentials(subscriptionKey),
                                new System.Net.Http.DelegatingHandler[] { }))
                            {
                                return await await contentRef.LoadStreamAsync(
                                    async (imageStream, contentType) =>
                                    {
                                        computerVision.Endpoint = endpointUri.OriginalString;
                                        var analysis = await computerVision.AnalyzeImageInStreamAsync(imageStream,
                                            VisualFeatureTypes.Categories.AsArray()
                                                .Append(VisualFeatureTypes.Description)
                                                .Append(VisualFeatureTypes.ImageType)
                                                .Append(VisualFeatureTypes.Objects)
                                                .Append(VisualFeatureTypes.Tags)
                                                .Append(VisualFeatureTypes.Brands)
                                                .ToList());
                                        return analysis;
                                    },
                                    () => throw new ResourceNotFoundException());
                            }
                        },
                            why => throw new ConfigurationException(
                                EastFive.Azure.AppSettings.CognitiveServices.ComputerVisionEndpoint,
                                typeof(string),
                                why,
                                typeof(ImageAnalysis)));
                },
                why => throw new ConfigurationException(
                    EastFive.Azure.AppSettings.CognitiveServices.ComputerVisionSubscriptionKey,
                    typeof(string),
                    why,
                    typeof(ImageAnalysis)));
        }
    }
}
