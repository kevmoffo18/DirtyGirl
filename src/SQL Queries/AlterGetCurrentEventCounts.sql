/****** Object:  StoredProcedure [dbo].[GetCurrentEventCounts]    Script Date: 4/26/2013 11:40:47 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER PROCEDURE [dbo].[GetCurrentEventCounts]
@EventId INT=0, @EventDate DATETIME=NULL
AS
BEGIN
 -- SET NOCOUNT ON added to prevent extra result sets from
 -- interfering with SELECT statements.
 SET NOCOUNT ON;
  Select evts.EventId, evts.IsActive
, evts.DateOfEvent
, evts.LastDateOfEvent
, evts.GeneralLocality
, evts.place
, evts.address1
, evts.locality
, evts.Name
, evts.code
, evts.postalcode
, evts.RegionID
, evts.maxregistrants	
, evts.RegistrationCount
, evts.PurchaseItemID
, evts.PinXCoordinate
, evts.PinYCoordinate    
, evts.RegistrationCutoff
, evts.EmailCutoff
, pi.cost
, ef.feeIconID
, fi.ImagePath
from (

select e.EventId, e.IsActive
,e.GeneralLocality, e.place, e.address1, e.locality, e.RegionID
, e.RegistrationCutoff
, e.EmailCutoff
, rg.Name
, rg.code
, e.postalcode,
(select min(ed3.DateOfEvent) from eventDate ed3 where ed3.EventId = e.EventId and ed3.IsActive = 1) as DateOfEvent,
(select max(ed3.DateOfEvent) from eventDate ed3 where ed3.EventId = e.EventId and ed3.IsActive = 1) as LastDateOfEvent,
(select sum(maxregistrants) from EventWave ew
inner join EventDate ed on ew.EventDateId = ed.EventDateId  
where ew.IsActive = 1  and ed.IsActive = 1 and  ed.EventId = e.EventId) as maxregistrants,
(Select Count(reg2.RegistrationId) As RegistrationCount
  From EventWave ew2 
  inner join Registration reg2 ON ew2.EventWaveId = reg2.EventWaveId
  inner join EventDate ed2 on ew2.EventDateId = ed2.EventDateId
  Where ed2.EventId = e.EventId  and ed2.IsActive = 1 and ew2.IsActive = 1 AND reg2.RegistrationId IS NOT NULL 
       AND (reg2.RegistrationStatus in (1,5)))  as RegistrationCount,
(select top (1) ef.PurchaseItemId from EventFee ef where ef.EventId = e.EventID and ef.EventFeeType = 1 and ef.EffectiveDate < GetDate() order by EffectiveDate desc ) as PurchaseItemID,
e.PinXCoordinate,
e.PinYCoordinate
from 
(select distinct eventid from EventDate ed4 where ed4.DateOfEvent > IsNull(@EventDate,getdate())) dts, Event e
inner join Region rg on rg.regionid = e.regionID 
where dts.EventId = e.EventId
and (e.EventID = @EventID or @EventID = 0) ) evts
inner join EventFee ef on ef.PurchaseItemId = evts.PurchaseItemID
inner join PurchaseItem pi on pi.PurchaseItemId = evts.PurchaseItemID
left outer join FeeIcon fi on fi.feeiconid = ef.feeIconID
              
END