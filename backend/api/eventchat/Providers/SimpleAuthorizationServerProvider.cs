﻿using eventchat.DAL;
using eventchat.Models;
using eventchat.Models.Repository;
using eventchat.Models.Wrappers;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.OAuth;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;

namespace eventchat.Providers
{
    public class SimpleAuthorizationServerProvider : OAuthAuthorizationServerProvider
    {
        // This one valides client - we only have one in this case
        public override async Task ValidateClientAuthentication(OAuthValidateClientAuthenticationContext context)
        {
            context.Validated();
        }

        // This one validates the username and password against the DB
        public override async Task GrantResourceOwnerCredentials(OAuthGrantResourceOwnerCredentialsContext context)
        {
            // Allow CORS
            context.OwinContext.Response.Headers.Add("Access-Control-Allow-Origin", new[] { "*" });
            User user;
            // Does username and password combination match?
            using (UserAuthRepository repo = new UserAuthRepository())
            {
                user = await repo.Find(context.UserName, context.Password);
                if (user == null)
                {
                    context.SetError("Invalid Credential", "The Username or Password is incorrect");
                    return;
                }
            }
           
            ClaimsIdentity identity = new ClaimsIdentity(context.Options.AuthenticationType);
            identity.AddClaim(new Claim("sub", context.UserName));
            identity.AddClaim(new Claim("role", "user"));

            AuthenticationProperties props = getUserProperties(user);
            AuthenticationTicket ticket = new AuthenticationTicket(identity, props);
            context.Validated(ticket);
        }

        public override Task TokenEndpoint(OAuthTokenEndpointContext context)
        {
            foreach (KeyValuePair<string, string> property in context.Properties.Dictionary)
            {
                context.AdditionalResponseParameters.Add(property.Key, property.Value);
            }

            return Task.FromResult<object>(null);
        }

        /// <summary>
        /// getUserProperties : AuthenticationProperties
        /// Loads the user information into response once authenticated
        /// </summary>
        /// <param name="user">User authenticated</param>
        /// <returns>AuthenticationProperties containing User information</returns>
        private AuthenticationProperties getUserProperties(User user)
        {
            AuthenticationProperties props = new AuthenticationProperties(new Dictionary<string, string>());
            props.Dictionary.Add("firstName", user.FirstName);
            props.Dictionary.Add("lastName", user.LastName);
            props.Dictionary.Add("address", user.Address);
            props.Dictionary.Add("userID", user.Id);
            props.Dictionary.Add("dataOfBirth", user.DateOfBirth.ToShortDateString());
            props.Dictionary.Add("userName", user.UserName);
            using (EventChatContext db = new EventChatContext())
            {
                List<UserSubscription> userSubscriptions = db.Subscriptions.
                    Where(x => x.subscribedUser.UserName.Equals(user.UserName)).
                    Select(x => new UserSubscription { UserName=user.UserName, targetUserName=x.subscriptionUser.UserName }).
                    ToList();
                props.Dictionary.Add("subscriptions", JsonConvert.SerializeObject(userSubscriptions));
            }
            return props;
        }
    }
}