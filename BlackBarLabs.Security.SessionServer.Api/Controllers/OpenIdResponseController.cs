//using System;
//using System.Collections.Generic;
//using System.IdentityModel.Tokens.Jwt;
//using System.Linq;
//using System.Web;
//using System.Web.Http;
//using System.Web.Mvc;

//namespace BlackBarLabs.Security.SessionServer.Api.Controllers
//{
//    public class OpenIdResponseXXXXController : Controller
//    {
        

//        // GET: OpenIdResponse
//        public ActionResult Index([FromBody]string id_token, [FromBody]string state)
//        {
//            if (!String.IsNullOrWhiteSpace(id_token))
//            {
//                var handler = new JwtSecurityTokenHandler();
//                var token = handler.ReadToken(id_token); // .Replace('-', '+').Replace('_', '/'));
//                var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters();
//                var qs =
//                    "538qDfJY4put42YpReCS32mEpMXabVKKP8alToo7MO0SgSvHABerybT1wK5fYXV_NkxIZrvQugwq0tCFdvAu9c1_ZM86u42mSOIvb05qH26SEusVwXNw_aFDFyccmmzjpK4_mv71fOqIVN3SAyUdVpDN6_gAPlh_ye_AjDKG7gUY_NaxwCKv_jTli_JuhOqS4jaop0cPq6StnhUMG9-Rn82i-bWY-4oQ8yWNY2In8J5sc2pkQDRYLBUgFsKKLVyA5u2XlftCn6OLDDrCk9yx4UK51_Dk1qLvzf4wArbi9GdkBkfVNx5OsrlDvVvgh8TOT1K22CLjXqC1YHzgpBOjdQ"
////                        "s4W7xjkQZP3OwG7PfRgcYKn8eRYXHiz1iK503fS-K2FZo-Ublwwa2xFZWpsUU_jtoVCwIkaqZuo6xoKtlMYXXvfVHGuKBHEBVn8b8x_57BQWz1d0KdrNXxuMvtFe6RzMqiMqzqZrzae4UqVCkYqcR9gQx66Ehq7hPmCxJCkg7ajo7fu6E7dPd34KH2HSYRsaaEA_BcKTeb9H1XE_qEKjog68wUU9Ekfl3FBIRN-1Ah_BoktGFoXyi_jt0-L0-gKcL1BLmUlGzMusvRbjI_0-qj-mc0utGdRjY-xIN2yBj8vl4DODO-wMwfp-cqZbCd9TENyHaTb8iA27s-73L3ExOQ"
//                        .Replace('-', '+').Replace('_', '/');
//                var modulus = KeyParamDecode(qs);
//                var keyParams = new System.Security.Cryptography.RSAParameters()
//                {
//                    Exponent = KeyParamDecode("AQAB"),
//                    Modulus = modulus,
//                };
//                var key1 = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(keyParams);
//                key1.KeyId = "gfIKIH-yZ3phRHRyjnsHIqZMaePLGAELzPat0CNY4sA";
//                validationParameters.IssuerSigningKey = key1;
//                validationParameters.ValidAudience = "51d61cbc-d8bd-4928-8abb-6e1bb315552";
//                validationParameters.ValidIssuer = "https://login.microsoftonline.com/eb96bf2a-bd94-48a4-ae9e-8331f19f3220/v2.0/";
//                Microsoft.IdentityModel.Tokens.SecurityToken validatedToken;
//                var claims = handler.ValidateToken(id_token, validationParameters, out validatedToken);
//            }

//            string longurl = "https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/oauth2/v2.0/authorize";
//            var uriBuilder = new UriBuilder(longurl);
//            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
//            query["client_id"] = "51d61cbc-d8bd-4928-8abb-6e1bb3155526";
//            query["response_type"] = "id_token";
//            query["redirect_uri"] = "http://localhost:6144/OpenIdResponse/";
//            query["response_mode"] = "form_post";
//            query["scope"] = "openid";
//            query["state"] = Guid.NewGuid().ToString("N");
//            query["nonce"] = "12345";
//            query["p"] = "B2C_1_signin1";
//            uriBuilder.Query = query.ToString();
//            var redirect = uriBuilder.ToString();
//            return Content(redirect); // View( );
//        }
//    }
//}
//// https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/oauth2/authorize?client_id=bb2a2e3a-c5e7-4f0a-88e0-8e01fd3fc1f4&redirect_uri=https:%2f%2flogin.microsoftonline.com%2fte%2fhumatestlogin.onmicrosoft.com%2foauth2%2fauthresp&response_type=id_token&scope=email+openid&response_mode=query&nonce=ZjJlb75S5AoaET4v6TLuxw%3d%3d&  nux=1&nca=1&domain_hint=humatestlogin.onmicrosoft.com&prompt=login&mkt=en-US&lc=1033&state=eyJTSUQiOiJ4LW1zLWNwaW0tcmM6NzJjNzQ2N2ItYTFiMi00MjdjLThlZTgtZDBmMTM3YjNlZGZkIiwiVElEIjoiNjMzZjdiZTktOTAxNy00ZDFkLWJjNWEtOTBmYWM3MWUxNWU3In0
//// https://login.microsoftonline.com/humatestlogin.onmicrosoft.com/oauth2/authorize?client_id=bb2a2e3a-c5e7-4f0a-88e0-8e01fd3fc1f4&redirect_uri=https:%2f%2flogin.microsoftonline.com%2fte%2fhumatestlogin.onmicrosoft.com%2foauth2%2fauthresp&response_type=id_token&scope=email+openid&response_mode=query&nonce=zEu4M5xhVG68UMNVV%2busug%3d%3d&nux=1&nca=1&domain_hint=humatestlogin.onmicrosoft.com&prompt=login&mkt=en-US&lc=1033&state=eyJTSUQiOiJ4LW1zLWNwaW0tcmM6ZWFmMzM0MWMtN2ZlOC00MjAxLWExYjgtN2QxMGEwM2M0MzQxIiwiVElEIjoiNjMzZjdiZTktOTAxNy00ZDFkLWJjNWEtOTBmYWM3MWUxNWU3In0

////https://login.microsoftonline.com/fabrikamb2c.onmicrosoft.com/oauth2/v2.0/authorize?
////client_id=90c0fe63-bcf2-44d5-8fb7-b8bbc0b29dc6
////&response_type=code+id_token
////&redirect_uri=https%3A%2F%2Faadb2cplayground.azurewebsites.net%2F
////&response_mode=form_post
////&scope=openid%20offline_access
////&state=arbitrary_data_you_can_receive_in_the_response
////&nonce=12345
////&p=b2c_1_sign_in
