using System;
using System.Runtime.Serialization;
using System.Web.Mvc;
using System.Web.Routing;

namespace NC2.Security.AuthorizationServer.API.Models
{
    [DataContract]
    public class User
    {
        #region Actionables
        public User() { }

        public User(Guid id, string email, bool isAuthenticated, UrlHelper urlHelper, RequestContext requestContext)
        {
            Id = id;
            Email = email;

            if (urlHelper.RouteCollection.Count > 0)
            {
                var route = urlHelper.RouteUrl("Default",
                    new {controller = "User", action = "Index", emailAddress = Email});
                SelfLink = new Uri(requestContext.HttpContext.Request.Url, route).AbsoluteUri;

                route = urlHelper.RouteUrl("Default", new {controller = "User", action = "Create"});
                CreateUser = new Uri(requestContext.HttpContext.Request.Url, route).AbsoluteUri;

                route = urlHelper.RouteUrl("Default", new { controller = "User", action = "UpdatePassword", emailAddress = Email});
                UpdatePassword = new Uri(requestContext.HttpContext.Request.Url, route).AbsoluteUri;

                route = urlHelper.RouteUrl("Default", new { controller = "User", action = "UpdateUser", emailAddress = Email });
                UpdateUser = new Uri(requestContext.HttpContext.Request.Url, route).AbsoluteUri;
            }
        }
        #endregion

        [DataMember]
        public Guid Id { get; set; }
       
        [DataMember]
        public string Email { get; set; }

        [DataMember]
        public string SelfLink { get; set; }

        [DataMember]
        public string CreateUser { get; set; }

        [DataMember]
        public string UpdatePassword { get; set; }

        [DataMember]
        public string UpdateUser { get; set; }
    }
}
