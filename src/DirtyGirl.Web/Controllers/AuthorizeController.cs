﻿using System;
using DirtyGirl.Models;
using DirtyGirl.Web.Models;
using System.Web.Mvc;
using System.Web.Security;


namespace DirtyGirl.Web.Controllers
{
    public class AuthorizeController : BaseController
    {
        #region Log In

        public ActionResult LogOn(string returnUrl)
        {
            return View(new vmLogon {ReturnUrl = returnUrl});
        }

        [HttpPost]
        public ActionResult LogOn(vmLogon model, string returnUrl)
        {
            if (ModelState.IsValid && ValidateUser(model.UserName, model.Password, model.RememberMe))
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && !model.ReturnUrl.Contains("logon"))       // if logging in, do not return to the logon screen
                    return RedirectToLocal(returnUrl);

                var user = UserService.GetUserByUsername(model.UserName); 
                if (user != null)
                    return RedirectToAction("viewuser", "user", new {userId = user.UserId});
            }

            ModelState.AddModelError("", "The user name or password provided is incorrect.");
            return View(model);
        }

        #endregion

        #region ModalLogon

        public ActionResult LogOnModal(string returnUrl)
        {
            vmLogon vm = new vmLogon {ReturnUrl = returnUrl};
            return PartialView("LoginModal", vm);
        }

        #endregion

        #region Logout

        public ActionResult LogOut()
        {
            Session.Clear();
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }

        #endregion

        public ActionResult ForgotPassword()
        {
            vmRedirect vm = new vmRedirect();
            vm.returnUrl = "/";
            return View(vm);
        }

        [HttpPost]
        public ActionResult ForgotPassword(string emailAddress)
        {
            vmRedirect vm = new vmRedirect();
            vm.returnUrl = "/";

            if (string.IsNullOrEmpty(emailAddress)) return View(vm);
            
            var result = UserService.GeneratePasswordResetRequest(emailAddress);
            ViewBag.Result = result;
            ViewBag.EmailAddress = emailAddress;

            return View(vm);
        }

        public ActionResult PasswordResetRequest(string token)
        {
            var vmPasswordReset = new vmPasswordReset();
            if (token == null) throw new ArgumentNullException("token");

            var user = UserService.GetUserByPasswordResetToken(token);

            if (user != null)
            {
                vmPasswordReset.User = user;
                vmPasswordReset.ResetToken = token;
                return View(vmPasswordReset);
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public ActionResult PasswordResetRequest(vmPasswordReset passwordReset)
        {
            var result = new ServiceResult();
            if (passwordReset == null) throw new ArgumentNullException("passwordReset");

            if (passwordReset.Password == passwordReset.ConfirmPassword && UserService.IsValidPasswordResetToken(passwordReset.ResetToken))
            {
                var user = UserService.GetUserByPasswordResetToken(passwordReset.ResetToken);
                result = UserService.UpdatePassword(user.UserId, passwordReset.Password);
                if (result.Success)
                    return RedirectToAction("Index", "Home");
                ModelState.AddModelError("Service Error", "Failed to update Password.  Please contact support.");
                return View(passwordReset);
            }
            ModelState.AddModelError("Password Mismatch", "Verify the passwords match");
            return View(passwordReset);
        }

        #region private

        private bool ValidateUser(string userName, string password, bool rememberMe)
        {
            if (Membership.Provider.ValidateUser(userName, password))
            {
                FormsAuthentication.SetAuthCookie(userName, rememberMe);
                return true;
            }

            return false;
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        #endregion

    }
}
