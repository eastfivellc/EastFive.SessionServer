using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Web.Mvc;
using System.Web.Routing;
using NC2.CPM.Web.Utilities.Extensions;
using NC2.CPM.Web.Utilities.Http;
using NC2.CPM.Web.Utilities.Routing;

namespace NC2.Security.AuthorizationServer.API.Models
{
    [DataContract]
    public class CredentialModel
    {
        #region Properties

        [DataMember]
        public Guid AuthenticationId { get; set; }

        [DataMember]
        public CredentialValidationMethodTypes Method { get; set; }

        [DataMember]
        public string UserId { get; set; }

        [DataMember]
        public Uri Provider { get; set; }

        [DataMember]
        public string Token { get; set; }

        #endregion
        
        #region Actionables
        public async Task<IEnumerable<CredentialModel>> ResolveAsync(Context context, HttpVerbs httpVerb, 
                                                                                     IResolveUrls routeResolver, IModifyResponse responseModifier)
        {
            throw new NotImplementedException();
        //    IBusinessSocialIdentity socialIdentity;
        //    var socialIdenties = new List<IBusinessSocialIdentity>();

        //    Go find the parent that this belongs to and make sure the parent exists.
        //    var userIdentity = await context.FindUserIdentityByIdAsync(UserIdentityId);
        //    if (userIdentity == null)
        //        throw this.PreconditionViewModelEntityNotFound();

        //    if (HttpVerbs.Get == httpVerb)
        //    {
        //        if (default(Guid) == Id)
        //        {
        //            socialIdenties.AddRange(await userIdentity.SocialIdentityAsync());
        //        }
        //        else
        //        {
        //            socialIdentity = await userIdentity.FindSocialIdentityByIdAsync(Id);
        //            if (null != socialIdentity)
        //            {
        //                socialIdenties.Add(socialIdentity);
        //            }
        //        }
        //        if (!socialIdenties.Any()) throw this.PreconditionViewModelEntityNotFound();

        //        var tasks = socialIdenties.Select(t => ToViewModelAsync(t, urlHelper, requestContext));
        //        return await Task.WhenAll(tasks);
        //    }

        //    socialIdentity = await userIdentity.FindSocialIdentityByIdAsync(Id);
        //    if (HttpVerbs.Post == httpVerb)
        //    {
        //        if (null != socialIdentity)
        //        {
        //            throw this.PreconditionViewModelEntityAlreadyExists();
        //        }

        //        await userIdentity.CreateSocialIdentityAsync(this.Id, this.AppId, this.Type, this.Method, this.UserId, this.Provider, this.Token);

        //        requestContext.SetResponseCode(HttpStatusCode.Created);
        //        requestContext.AddLocationHeader(requestContext.GetEndPointUrl(urlHelper, "SocialIdentity", Id));
        //        return new SocialIdentityModel[] { };

        //    }

        //    if (HttpVerbs.Put == httpVerb)
        //    {
        //        if (null == socialIdentity)
        //        {
        //            throw this.PreconditionViewModelEntityNotFound();
        //        }

        //        await socialIdentity.UpdateToken(this.Token);

        //        requestContext.SetResponseCode(HttpStatusCode.NoContent);
        //        requestContext.AddLocationHeader(requestContext.GetEndPointUrl(urlHelper, "SocialIdentity", Id));
        //        return new SocialIdentityModel[] { };
        //    }
            
        }

        private static async Task<SocialIdentityModel> ToViewModelAsync(AuthorizationServer.Business.SocialIdentity.SocialIdentity socialIdentity, UrlHelper urlHelper, RequestContext requestContext)
        {
            var viewModel = new SocialIdentityModel ();
            /*
                await socialIdentity.ExtrudeInformationAsync((id, appId, type, method, userid, provider, token) =>
                {
                    viewModel.Method = method;
                    viewModel.UserId = userid;
                    viewModel.Provider = provider;
                    viewModel.Token = token;
                });
            */
            return viewModel;
        }
        
        #endregion

    }
}
