﻿using AutoMapper;
using ClassifiedAds.Application;
using ClassifiedAds.Application.Users.Commands;
using ClassifiedAds.Application.Users.Queries;
using ClassifiedAds.CrossCuttingConcerns.OS;
using ClassifiedAds.Domain.Entities;
using ClassifiedAds.WebAPI.ConfigurationOptions;
using ClassifiedAds.WebAPI.Models.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace ClassifiedAds.WebAPI.Controllers
{
    [Authorize]
    [Produces("application/json")]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly Dispatcher _dispatcher;
        private readonly UserManager<User> _userManager;
        private readonly IMapper _mapper;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly AppSettings _appSettings;

        public UsersController(Dispatcher dispatcher,
            UserManager<User> userManager,
            ILogger<UsersController> logger,
            IMapper mapper,
            IDateTimeProvider dateTimeProvider,
            IOptionsSnapshot<AppSettings> appSettings)
        {
            _dispatcher = dispatcher;
            _userManager = userManager;
            _mapper = mapper;
            _dateTimeProvider = dateTimeProvider;
            _appSettings = appSettings.Value;
        }

        [HttpGet]
        public ActionResult<IEnumerable<User>> Get()
        {
            var users = _dispatcher.Dispatch(new GetUsersQuery());
            var model = _mapper.Map<List<UserModel>>(users);
            return Ok(model);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult<User> Get(Guid id)
        {
            var user = _dispatcher.Dispatch(new GetUserQuery { Id = id, AsNoTracking = true });
            var model = _mapper.Map<UserModel>(user);
            return Ok(model);
        }

        [HttpPost]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<ActionResult<User>> Post([FromBody] UserModel model)
        {
            User user = new User
            {
                UserName = model.UserName,
                NormalizedUserName = model.UserName.ToUpper(),
                Email = model.Email,
                NormalizedEmail = model.Email.ToUpper(),
                EmailConfirmed = model.EmailConfirmed,
                PhoneNumber = model.PhoneNumber,
                PhoneNumberConfirmed = model.PhoneNumberConfirmed,
                TwoFactorEnabled = model.TwoFactorEnabled,
                LockoutEnabled = model.LockoutEnabled,
                LockoutEnd = model.LockoutEnd,
                AccessFailedCount = model.AccessFailedCount,
            };

            _ = await _userManager.CreateAsync(user);

            model = _mapper.Map<UserModel>(user);
            return Created($"/api/users/{model.Id}", model);
        }

        [HttpPut("{id}")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Put(Guid id, [FromBody] UserModel model)
        {
            User user = _dispatcher.Dispatch(new GetUserQuery { Id = id });

            user.UserName = model.UserName;
            user.NormalizedUserName = model.UserName.ToUpper();
            user.Email = model.Email;
            user.NormalizedEmail = model.Email.ToUpper();
            user.EmailConfirmed = model.EmailConfirmed;
            user.PhoneNumber = model.PhoneNumber;
            user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;
            user.TwoFactorEnabled = model.TwoFactorEnabled;
            user.LockoutEnabled = model.LockoutEnabled;
            user.LockoutEnd = model.LockoutEnd;
            user.AccessFailedCount = model.AccessFailedCount;

            _ = await _userManager.UpdateAsync(user);

            model = _mapper.Map<UserModel>(user);
            return Ok(model);
        }

        [HttpPut("{id}/password")]
        [Consumes("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> SetPassword(Guid id, [FromBody] UserModel model)
        {
            User user = _dispatcher.Dispatch(new GetUserQuery { Id = id });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var rs = await _userManager.ResetPasswordAsync(user, token, model.Password);

            if (rs.Succeeded)
            {
                return Ok();
            }

            return BadRequest(rs.Errors);
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public ActionResult Delete(Guid id)
        {
            var user = _dispatcher.Dispatch(new GetUserQuery { Id = id });
            _dispatcher.Dispatch(new DeleteUserCommand { User = user });

            return Ok();
        }

        [HttpPost("{id}/passwordresetemail")]
        public async Task<ActionResult> SendResetPasswordEmail(Guid id)
        {
            User user = _dispatcher.Dispatch(new GetUserQuery { Id = id });

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetUrl = $"{_appSettings.IdentityServerAuthentication.Authority}/Account/ResetPassword?token={HttpUtility.UrlEncode(token)}&email={user.Email}";

                _dispatcher.Dispatch(new AddOrUpdateEntityCommand<EmailMessage>(new EmailMessage
                {
                    From = "phong@gmail.com",
                    Tos = user.Email,
                    Subject = "Forgot Password",
                    Body = string.Format("Reset Url: {0}", resetUrl),
                }));
            }
            else
            {
                // email user and inform them that they do not have an account
            }

            return Ok();
        }

        [HttpPost("{id}/emailaddressconfirmation")]
        public async Task<ActionResult> SendConfirmationEmailAddressEmail(Guid id)
        {
            User user = _dispatcher.Dispatch(new GetUserQuery { Id = id });

            if (user != null)
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

                var confirmationEmail = $"{_appSettings.IdentityServerAuthentication.Authority}/Account/ConfirmEmailAddress?token={HttpUtility.UrlEncode(token)}&email={user.Email}";

                _dispatcher.Dispatch(new AddOrUpdateEntityCommand<EmailMessage>(new EmailMessage
                {
                    From = "phong@gmail.com",
                    Tos = user.Email,
                    Subject = "Confirmation Email",
                    Body = string.Format("Confirmation Email: {0}", confirmationEmail),
                }
                ));
            }
            else
            {
                // email user and inform them that they do not have an account
            }

            return Ok();
        }
    }
}