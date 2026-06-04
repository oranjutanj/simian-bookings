# About me
My name is Mike and I have a M365 for business account for the company "Simian Coaching" (https://simiancoaching.co.uk/ - a wordpress site that I look after on the premium plan. Difficult if not impossible to use plugins). I am the only "employee" at simian coaching and therefore have admin access to everything.

I have a day job at Waterstons, which is the laptop I will be using, but can also switch between M365 accounts. I do coaching sessions of 45 minute or 60 minute through the week on an evening. I also do mentorship sessions through ADPList (https://adplist.org/) which has the perfect setup - it syncs with my google calendar and my simian coaching outlook calendar and checks for availablity when people are booking a session. The sessions are configurable, so that I can set things like length, description, gaps in between meetings, etc. Mentees sign up to the platform and can book any time with me based on these settings. It will create a meeting in my google calendar.

I am a software engineering manager, I can sort of code, especially vibe code. I am IT literate, and could throw together some stuff in power platform or even host a basic app in azure but I'd want it all to have simiancoaching branding or affiliation.

I am very tight and DO NOT want to pay for any more third party services. I don't charge for my coaching so it's all volunteering, and I already pay the subscription to M365 business, the price for the domain, and the cost of hosting the website. I can tolerate small hosting costs in Azure. If I was to pick any language I was comfortable in it would be C#.

# What I want
I want to be able to replicate a similar thing with my Simian Coaching business. I had a golden period where I could use Microsoft Bookings, where I had the company bookings page available (https://bookings.cloud.microsoft/book/Hello@simiancoaching.co.uk/?ismsaljsauthenabled=true) to book a variety of sessions similar to ADPList. I had connected my Google account on Outlook for Web and everything worked like I wanted - I received a meeting invite through my simian coaching outlook, it set up a Teams meeting for me, it honoured both my outlook and Google calendars which is the way it was supposed to work. 
a
Before this, I had tried "Book Like a Boss" and even paid for an enterprise plan so that it would work for me. When I found the google integrate function on outlook for web I cancelled my subscription. I didn't like this third party approach as I can't customise it too much without paying a fortune and it's just jarring to see different branded things from the website.

Microsoft Bookings itself still works fine and is still available to me. The problem is the Google Calendar integration within Outlook for Web, which Bookings relies on to check for conflicts across both calendars. This integration has stopped working — Googling it suggests it is flakey and unreliable for many users. As a result, people can still book sessions through the Bookings page, but Bookings is unaware of my personal Google Calendar appointments, so I end up with double-bookings and conflicts.

## Useful info
- I have maybe 1 booking a week, so very low traffic

# My Options
- Integration - having real-time (or near real time sync) with events using a third party. i do not want this. i COULD build something with AI assistance, but this seems very faffy.
- Build from scratch - host a very simple website with simian coaching brnding, on the simiancoaching.co.uk domain, for bookings. This option is  attractive but seems a lot of work.
- Something utilising powr platform or power apps maybe? As long as it doesn't cost anything xtra.
- wait until Microsoft fix the Google integration thing - I have no idea how long that will be.
- Point people to google calendar? Seems to defeat the object of having any company branding but it'sa temporary measure

## AI Suggested Options
[AI to insert suggestions here]
- **WordPress customisation**: Premium plan can't run custom JS calling external APIs - need separate hosting for booking page
- **Power Automate sync** - Rejected: Would create duplicate events in Google Calendar (already subscribing to Outlook calendar)
- **Pre/post-booking checkers** - Rejected: Poor user experience, manual intervention required

## Chosen Solution: Custom Azure Function + Static Booking Page

### Architecture
- **Frontend**: Static HTML/CSS/JS booking page hosted on book.simiancoaching.co.uk (Azure Static Web Apps or Blob Storage)
- **Backend**: C# Azure Functions (consumption plan - free at my volume)
- **Configuration**: JSON file (sessions.json) - simple, version controlled, no redeployment needed for rare changes
- **APIs**: Microsoft Graph (Outlook calendar) + Google Calendar API
- **Cost**: ~£0/month at 1 booking per week

### How It Works
1. User visits booking page, selects session type and time
2. Azure Function checks BOTH Outlook and Google calendars for conflicts via APIs
3. If available, creates booking in Outlook calendar (auto-generates Teams meeting)
4. No ongoing sync required - just real-time conflict checking at booking time

### Prerequisites
- M365 app registration in Simian Coaching tenant (have admin access ✅)
- Google Cloud Console project + OAuth setup (free, one-time setup)
- Azure subscription for hosting (already have)

### Why This Solution
- ✅ Full control and customisation (C# comfort zone)
- ✅ Reliable (direct API calls, no flaky integrations)
- ✅ Fast performance (<2s response time)
- ✅ Simian Coaching branding throughout
- ✅ No third-party subscription costs
- ✅ No duplicate calendar entries (unlike Power Automate sync approach)
- ✅ Better UX than manual confirmation or pre-booking checkers

### Build Estimate
2-3 weekends with AI assistance

### Key Components
- GetAvailableSlots function (checks both calendars, returns free slots)
- CreateBooking function (double-checks availability, creates Outlook event)
- Simple booking form UI with calendar picker
- sessions.json for session types, durations, availability windows

### Rejected Alternatives
- Power Automate sync: Would create duplicates (already subscribe to Outlook in Google)
- Pre/post-booking checkers: Poor UX, manual intervention
- Waiting for Microsoft to fix Google integration: Unreliable timeline
- Third-party services: Ongoing costs, limited customisation

### Instructions to keep progress updated
All progress should be updated by AI assistants in the status.md file in this workspace, with date, progress and next steps. irrelevant progress can be deleted to save bloat. It should create enough information to be able to pick up where we left off.

### Assets and colours that will be useful
Simian coaching logos are in the assets folder. You may need this when adding logos.
The colour scheme for simian coaching is a tea green scheme and is as follows:
- Dark #157266
- Med #0EB69F
- Light #BCE1C6