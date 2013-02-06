﻿using DirtyGirl.Models;
using DirtyGirl.Models.Enums;
using DirtyGirl.Services.ServiceInterfaces;
using DirtyGirl.Web.Helpers;
using DirtyGirl.Web.Models;
using DirtyGirl.Web.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;

namespace DirtyGirl.Web.Controllers
{
    [Authorize(Roles = "Registrant, Admin, SuperUser")]
    public class RegistrationController : BaseController
    {

        #region Constructor

        IRegistrationService _service;

        public RegistrationController(IRegistrationService service)
        {
            this._service = service;
        }

        #endregion

        #region EventSelection

        public ActionResult EventSelection(Guid itemId, int? eventId, int? eventDateId, int? eventWaveId)
        {
            vmRegistration_EventSelection vm = new vmRegistration_EventSelection
            {
                ItemId = itemId,
                CartFocus = SessionManager.CurrentCart.CheckOutFocus               
            };

            if (eventWaveId.HasValue)
            {
                EventWave selectedWave = _service.GetEventWaveById(eventWaveId.Value);
                vm.EventId = selectedWave.EventDate.EventId;
                vm.EventDateId = selectedWave.EventDateId;
                vm.EventWaveId = selectedWave.EventWaveId;
            }
            else if(eventDateId.HasValue)
            {
                EventDate selectedDate = _service.GetEventDateById(eventDateId.Value);
                vm.EventId = selectedDate.EventId;
                vm.EventDateId = selectedDate.EventDateId;
            }
            else if (eventId.HasValue)
            {
                Event selectedEvent = _service.GetEventById(eventId.Value);
                vm.EventId = eventId.Value;
                vm.EventDateId = selectedEvent.EventDates.First().EventDateId;
                vm.EventName = selectedEvent.GeneralLocality;
            }

            if (SessionManager.CurrentCart.ActionItems[itemId].ActionType == CartActionType.WaveChange)
                vm.LockEvent = true;


            return View(vm);
        }

        [HttpPost]
        public ActionResult WaveSelected(int eventWaveId, Guid itemId)
        {
            var action = SessionManager.CurrentCart.ActionItems[itemId];

            switch (action.ActionType)
            {
                case CartActionType.NewRegistration:
                    
                    ((Registration)action.ActionObject).EventWaveId = eventWaveId;                          
                    return RedirectToAction("CreateTeam", new { itemId});

                case CartActionType.EventChange:

                    ((ChangeEventAction)action.ActionObject).UpdatedEventWaveId = eventWaveId;
                    action.ItemReadyForCheckout = true;
                    return RedirectToAction("checkout", "cart");

                case CartActionType.WaveChange:                    
                    
                    ServiceResult waveChangeResult = _service.ChangeWave(((ChangeWaveAction)action.ActionObject).RegistrationId, eventWaveId);

                    if (waveChangeResult.Success)
                    {
                        SessionManager.CurrentCart.ActionItems.Remove(itemId);
                        DisplayMessageToUser(new DisplayMessage(DisplayMessageType.SuccessMessage, "You have successfully updated your event wave."));
                        return RedirectToAction("viewuser", "user", new { username = CurrentUser.UserName });                        
                    }                                           
                    break;
            }

            return RedirectToAction("EventSelection", new {eventWaveId, itemId});
        }

        public JsonResult GetEventList()
        {
            return Json(_service.GetActiveUpcomingEvents().Select(x => new { EventId = x.EventId, Name = x.EndDate > x.StartDate ? string.Format("{0}, {1} : {2} - {3}", x.GeneralLocality, x.StateCode, x.StartDate.ToShortDateString(), x.EndDate.ToShortDateString()) : string.Format("{0}, {1} : {2}", x.GeneralLocality, x.StateCode, x.StartDate.ToShortDateString()) }).ToList(), JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetEventDateList(int eventId)
        {
            return Json(_service.GetActiveDateDetailsByEvent(eventId).Select(x => new { EventDateId = x.EventDateId, DateOfEvent = x.DateOfEvent.ToShortDateString() }).ToList(), JsonRequestBehavior.AllowGet);
        }

        public ActionResult GetWavesByEventDateId(int eventDateId)
        {
            var Waves = _service.GetWaveDetialsForEventDate(eventDateId);

            vmRegistration_WaveList vm = new vmRegistration_WaveList();
            vm.MorningWaves = GetWaveItems(Waves.Where(x => x.StartTime.ToString("tt") == "AM").ToList());
            vm.EveningWaves = GetWaveItems(Waves.Where(x => x.StartTime.ToString("tt") == "PM").ToList());

            return PartialView("Partial/WaveList", vm);
        }

        private List<vmRegistration_WaveItem> GetWaveItems(List<EventWaveDetails> waveOverviewList)
        {

            List<vmRegistration_WaveItem> waveItemList = new List<vmRegistration_WaveItem>();

            foreach (var wave in waveOverviewList)
            {
                vmRegistration_WaveItem newWave = new vmRegistration_WaveItem();
                newWave.EventWaveId = wave.EventWaveId;
                newWave.WaveNumber = waveOverviewList.IndexOf(wave) + 1;
                newWave.StartTime = wave.StartTime;
                newWave.isFull = wave.SpotsLeft <= 0;

                if (wave.SpotsLeft <= 0)
                {
                    newWave.WaveNotification = "SOLD OUT!";
                    newWave.cssClassName = "full_wave";
                }
                else if (wave.SpotsLeft < DirtyGirlConfig.Settings.DisplaySpotsAvailableCount)
                {
                    newWave.WaveNotification = string.Format("{0} spots left", wave.SpotsLeft);
                    newWave.cssClassName = "half_wave";
                }
                else
                {
                    newWave.WaveNotification = "spots open";
                    newWave.cssClassName = "empty_wave";
                }

                waveItemList.Add(newWave);
            }

            return waveItemList;

        }

        #endregion

        #region Registration

        public ActionResult RegistrationDetails(Guid itemId) 
        {
            var regAction = SessionManager.CurrentCart.ActionItems[itemId];
            var reg = (Registration)regAction.ActionObject;
            var wave = _service.GetEventWaveById(reg.EventWaveId);
            int eventId = wave.EventDate.EventId;

            var vm = new vmRegistration_Details 
                { 

                    EventWave = _service.GetEventWaveById(reg.EventWaveId),
                    EventOverview = _service.GetEventOverviewById(eventId),
                    RegionList = _service.GetRegionsByCountry(DirtyGirlConfig.Settings.DefaultCountryId),
                    RegistrationTypeList = DirtyGirlExtensions.ConvertToSelectList<RegistrationType>(),
                    EventLeadList = _service.GetEventLeads(eventId, true),
                    RegistrationDetails = reg,
                    ItemId = itemId
                };          

            return View(vm);
        }

        [HttpPost]
        public ActionResult RegistrationDetails(vmRegistration_Details model)
        {
            var regAction = SessionManager.CurrentCart.ActionItems[model.ItemId];
            var reg = (Registration)regAction.ActionObject;

            if (ModelState.IsValid)
            {
                reg.Address1 = model.RegistrationDetails.Address1;
                reg.Address2 = model.RegistrationDetails.Address2;
                reg.AgreeToTerms = model.RegistrationDetails.AgreeToTerms;
                reg.CartItemId = model.RegistrationDetails.CartItemId;
                reg.DateAdded = model.RegistrationDetails.DateAdded;
                reg.Email = model.RegistrationDetails.Email;
                reg.EmergencyContact = model.RegistrationDetails.EmergencyContact;
                reg.EmergencyPhone = model.RegistrationDetails.EmergencyPhone;
                reg.EventLeadId = model.RegistrationDetails.EventLeadId;
                reg.FirstName = model.RegistrationDetails.FirstName;
                reg.Gender = model.RegistrationDetails.Gender;
                reg.IsFemale = model.RegistrationDetails.IsFemale;
                reg.IsOfAge = model.RegistrationDetails.IsOfAge;
                reg.IsThirdPartyRegistration = model.RegistrationDetails.IsThirdPartyRegistration;
                reg.LastName = model.RegistrationDetails.LastName;
                reg.Locality = model.RegistrationDetails.Locality;
                reg.MedicalInformation = model.RegistrationDetails.MedicalInformation;
                reg.ParentRegistrationId = model.RegistrationDetails.ParentRegistrationId;
                reg.Phone = model.RegistrationDetails.Phone;
                reg.PostalCode = model.RegistrationDetails.PostalCode;
                reg.ReferenceAnswer = model.RegistrationDetails.ReferenceAnswer;
                reg.RegionId = model.RegistrationDetails.RegionId;
                reg.RegistrationStatus = RegistrationStatus.Active;
                reg.RegistrationType = model.RegistrationDetails.RegistrationType;
                reg.SpecialNeeds = model.RegistrationDetails.SpecialNeeds;
                reg.TeamId = model.RegistrationDetails.TeamId;
                reg.UserId = CurrentUser.UserId;

                regAction.ActionObject = reg;
                regAction.ItemReadyForCheckout = true;

                return RedirectToAction("checkout", "cart");
            }

            EventWave wave = _service.GetEventWaveById(reg.EventWaveId);

            model.EventWave = wave;
            model.EventOverview = _service.GetEventOverviewById(wave.EventDate.EventId);
            model.RegionList = _service.GetRegionsByCountry(DirtyGirlConfig.Settings.DefaultCountryId);
            model.RegistrationTypeList = DirtyGirlExtensions.ConvertToSelectList<RegistrationType>();
            model.EventLeadList = _service.GetEventLeads(wave.EventDate.EventId, true);         

            return View(model);
        }        
        
        #endregion

        #region Create Team

        public ActionResult CreateTeam(Guid itemId)
        {      
            
            var reg = (Registration)SessionManager.CurrentCart.ActionItems[itemId].ActionObject;
            var wave = _service.GetEventWaveById(reg.EventWaveId);

            var vm = new vmRegistration_CreateTeam
                {
                    ItemId = itemId,
                    EventId = wave.EventDate.EventId,
                    EventOverview = _service.GetEventOverviewById(wave.EventDate.EventId)
                };         

            if (reg.TeamId.HasValue)
            {
                vm.RegistrationType = "team";
                vm.TeamType = "existing";
                vm.TeamCode = _service.GetTeamById(reg.TeamId.Value).Code;
            }
            else if (reg.Team != null)
            {
                vm.RegistrationType = "team";
                vm.TeamType = "new";
                vm.TeamName = reg.Team.Name;
            }
            else
            {
                vm.RegistrationType = "individual";
                vm.TeamType = "new";
            }

            return View(vm);
        }

        [HttpPost]
        public ActionResult CreateTeam(vmRegistration_CreateTeam model)
        {           
            if (ModelState.IsValid)
            {
                var reg = (Registration)SessionManager.CurrentCart.ActionItems[model.ItemId].ActionObject;

                if (model.RegistrationType.ToLower() == "team")
                {
                    switch (model.TeamType.ToLower())
                    {
                        case "existing":

                            if (string.IsNullOrEmpty(model.TeamCode))
                                ModelState.AddModelError("TeamCode", "Team Code is Required.");
                            else
                            {
                                var existingTeam = _service.GetTeamByCode(model.EventId, model.TeamCode);

                                if (existingTeam != null)
                                    reg.TeamId = existingTeam.TeamId;
                                else
                                    ModelState.AddModelError("TeamCode", "There is no team using this code for this event.");
                            }
                            break;

                        case "new":
                            if (string.IsNullOrEmpty(model.TeamName))
                                ModelState.AddModelError("TeamName", "Team Name is Required");
                            else
                            {
                                Team newTeam = new Team { EventId = model.EventId, Name = model.TeamName, CreatorID = CurrentUser.UserId };

                                ServiceResult tempTeamResult = _service.GenerateTempTeam(newTeam);

                                if (!tempTeamResult.Success)
                                    Utilities.AddModelStateErrors(ModelState, tempTeamResult.GetServiceErrors());
                                else
                                    reg.Team = newTeam;
                            }
                            break;
                    }
                }
                else
                {
                    reg.Team = null;
                    reg.TeamId = null;
                }                

                if (ModelState.IsValid)
                    return RedirectToAction("RegistrationDetails", new { itemId = model.ItemId });
                
            }

            model.EventOverview = _service.GetEventOverviewById(model.EventId);
            return View(model);
        }

        public JsonResult ValidateTeamCode(int eventId, string code)
        {
            var team = _service.GetTeamByCode(eventId, code);
            return team != null ? 
                Json(new { Name = team.Name }, JsonRequestBehavior.AllowGet) : 
                Json(new {Status = "Failure"}, JsonRequestBehavior.AllowGet);
        }

        #endregion        

        #region Transfer

        public ActionResult Transfer(Guid itemId)
        {
            TransferAction action = (TransferAction)SessionManager.CurrentCart.ActionItems[itemId].ActionObject;            
            Registration existingReg = _service.GetRegistrationById(action.RegistrationId);

            var vm = new vmRegistration_Transfer
                {
                    ItemId = itemId,
                    FirstName = action.FirstName,
                    LastName = action.LastName,
                    Email = action.Email,
                    EventOverview = _service.GetEventOverviewById(existingReg.EventWave.EventDate.EventId)
                };           
            
            return View(vm);
        }

        [HttpPost]
        public ActionResult Transfer(vmRegistration_Transfer model)
        {
            var action = SessionManager.CurrentCart.ActionItems[model.ItemId];
            var transfer = (TransferAction)action.ActionObject;

            if (ModelState.IsValid)
            {                
                transfer.FirstName = model.FirstName;
                transfer.LastName = model.LastName;
                transfer.Email = model.Email;
                action.ItemReadyForCheckout = true;

                return RedirectToAction("checkout", "cart");
            }

            Registration existingReg = _service.GetRegistrationById(transfer.RegistrationId);
            model.EventOverview = _service.GetEventOverviewById(existingReg.EventWave.EventDate.EventId);          

            return View(model);
        }

        #endregion
        
    }
}
