// Of COURSE this is vibe coded.

(() => {
  const configuredApiBase = (window.SIMIAN_CONFIG?.apiBase || "").trim();
  const defaultProdApiBase = "https://simian-bookings-api-d8hgfzhxa8fjbwfv.ukwest-01.azurewebsites.net/api";

  const API_BASE =
    configuredApiBase
      ? configuredApiBase.replace(/\/+$/, "")
      : window.location.protocol === "file:" ||
        window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1"
      ? "http://localhost:7071/api"
      : defaultProdApiBase;

  const state = {
    sessions: [],
    selectedSession: null,
    allSlots: [],
    selectedSlot: null,
    weekOffset: 0,
    userTimeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC"
  };

  const els = {
    sessionList: document.getElementById("session-list"),
    chooseTimeButton: document.getElementById("btn-to-step-2"),
    continueButton: document.getElementById("btn-to-step-3"),
    slotsContainer: document.getElementById("slots-container"),
    prevWeekButton: document.getElementById("btn-prev-week"),
    nextWeekButton: document.getElementById("btn-next-week"),
    weekLabel: document.getElementById("week-label"),
    timeZoneNote: document.getElementById("timezone-note"),
    bookingSummary: document.getElementById("booking-summary"),
    formError: document.getElementById("form-error"),
    confirmButton: document.getElementById("btn-confirm"),
    confirmText: document.getElementById("confirm-text"),
    teamsLinkButton: document.getElementById("teams-link-btn"),
    inputName: document.getElementById("input-name"),
    inputEmail: document.getElementById("input-email"),
    inputMessage: document.getElementById("input-message"),
    stepOneBackButton: document.getElementById("btn-back-to-step-1"),
    stepTwoBackButton: document.getElementById("btn-back-to-step-2")
  };

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function formatDay(date) {
    return date.toLocaleDateString(undefined, {
      weekday: "long",
      day: "numeric",
      month: "long"
    });
  }

  function formatTime(date, includeZone = false) {
    return date.toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
      timeZoneName: includeZone ? "short" : undefined
    });
  }

  function formatDateTime(date, includeZone = false) {
    return date.toLocaleString(undefined, {
      weekday: "long",
      day: "numeric",
      month: "long",
      hour: "2-digit",
      minute: "2-digit",
      timeZoneName: includeZone ? "short" : undefined
    });
  }

  function setTimeZoneHint() {
    els.timeZoneNote.textContent = `Times are shown in your local timezone (${state.userTimeZone}). `;
  }

  function getCurrentWeekRange() {
    const now = new Date();
    // Anchor on UTC midnight to avoid week-boundary drift for users in western timezones
    // (e.g. US Pacific at 11pm local is already the next UTC day, which would shift the window)
    const weekStart = new Date(Date.UTC(
      now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()
    ));
    weekStart.setUTCDate(weekStart.getUTCDate() + state.weekOffset * 7);

    const weekEnd = new Date(weekStart);
    weekEnd.setUTCDate(weekStart.getUTCDate() + 7);

    return { weekStart, weekEnd };
  }

  async function loadSessions() {
    try {
      const response = await fetch(`${API_BASE}/session-types`);
      if (!response.ok) {
        throw new Error("Session request failed");
      }

      state.sessions = await response.json();
      renderSessions();
    } catch {
      els.sessionList.innerHTML =
        '<div class="error-msg">Could not load sessions. Please refresh the page.</div>';
    }
  }

  function renderSessions() {
    els.sessionList.innerHTML = state.sessions
      .map(
        (session) => `
      <button type="button" class="session-card" data-id="${escapeHtml(session.id)}" aria-label="${escapeHtml(session.name)}">
        <span class="duration">${escapeHtml(session.durationMinutes)} min</span>
        <h3>${escapeHtml(session.name)}</h3>
        <p>${escapeHtml(session.description)}</p>
      </button>
    `
      )
      .join("");
  }

  function selectSession(id) {
    const session = state.sessions.find((item) => item.id === id);
    if (!session) {
      return;
    }

    state.selectedSession = session;
    state.selectedSlot = null;
    els.continueButton.disabled = true;

    document.querySelectorAll(".session-card").forEach((card) => {
      card.classList.toggle("selected", card.dataset.id === id);
    });

    els.chooseTimeButton.disabled = false;
  }

  function getWeekSlots() {
    const { weekStart, weekEnd } = getCurrentWeekRange();
    return state.allSlots.filter((slot) => {
      const slotDate = new Date(slot);
      return slotDate >= weekStart && slotDate < weekEnd;
    });
  }

  function updateWeekLabel() {
    const { weekStart } = getCurrentWeekRange();
    const weekEnd = new Date(weekStart);
    weekEnd.setUTCDate(weekStart.getUTCDate() + 6);

    const startText = weekStart.toLocaleDateString(undefined, { day: "numeric", month: "short" });
    const endText = weekEnd.toLocaleDateString(undefined, { day: "numeric", month: "short" });

    els.weekLabel.textContent = `${startText} - ${endText}`;
    els.prevWeekButton.disabled = state.weekOffset <= 0;
  }

  function renderSlots() {
    updateWeekLabel();

    const weekSlots = getWeekSlots();
    if (weekSlots.length === 0) {
      els.slotsContainer.innerHTML =
        '<p class="no-slots">No available slots this week. Try the next week.</p>';
      return;
    }

    const groupedSlots = new Map();
    for (const slot of weekSlots) {
      const date = new Date(slot);
      const key = formatDay(date);
      if (!groupedSlots.has(key)) {
        groupedSlots.set(key, []);
      }

      groupedSlots.get(key).push(slot);
    }

    const groupedHtml = Array.from(groupedSlots.entries())
      .map(
        ([dayLabel, slots]) => `
      <div class="day-group">
        <div class="day-label">${escapeHtml(dayLabel)}</div>
        <div class="slots-row">
          ${slots
            .map(
              (slot) => `
            <button type="button" class="slot-btn${slot === state.selectedSlot ? " selected" : ""}" data-slot="${escapeHtml(slot)}">
              ${escapeHtml(formatTime(new Date(slot)))}
            </button>
          `
            )
            .join("")}
        </div>
      </div>
    `
      )
      .join("");

    els.slotsContainer.innerHTML = groupedHtml;
  }

  function selectSlot(slotUtc) {
    state.selectedSlot = slotUtc;
    document.querySelectorAll(".slot-btn").forEach((button) => {
      button.classList.toggle("selected", button.dataset.slot === slotUtc);
    });

    els.continueButton.disabled = false;
  }

  async function loadSlots() {
    if (!state.selectedSession) {
      return;
    }

    els.slotsContainer.innerHTML =
      '<div class="loading"><div class="spinner"></div><br/>Checking availability...</div>';

    try {
      const response = await fetch(
        `${API_BASE}/slots?sessionType=${encodeURIComponent(state.selectedSession.id)}&weeksAhead=4`
      );

      if (!response.ok) {
        throw new Error("Slots request failed");
      }

      const data = await response.json();
      state.allSlots = data.availableSlots ?? [];
      state.selectedSlot = null;
      state.weekOffset = 0;
      els.continueButton.disabled = true;
      renderSlots();
    } catch {
      els.slotsContainer.innerHTML =
        '<div class="error-msg">Could not load availability. Please try again.</div>';
    }
  }

  function renderBookingSummary() {
    if (!state.selectedSession || !state.selectedSlot) {
      return;
    }

    const start = new Date(state.selectedSlot);
    const end = new Date(start.getTime() + state.selectedSession.durationMinutes * 60000);

    els.bookingSummary.innerHTML = `
      <strong>${escapeHtml(state.selectedSession.name)}</strong><br/>
      ${escapeHtml(formatDateTime(start, true))} - ${escapeHtml(formatTime(end))}
    `;
  }

  function goTo(step) {
    if (step === 2 && !state.selectedSession) {
      return;
    }

    if (step === 3 && !state.selectedSlot) {
      return;
    }

    document.querySelectorAll(".step").forEach((element, index) => {
      element.classList.toggle("active", index + 1 === step);
    });

    if (step === 2) {
      loadSlots();
    }

    if (step === 3) {
      renderBookingSummary();
    }
  }

  function resetConfirmButton() {
    els.confirmButton.disabled = false;
    els.confirmButton.textContent = "Confirm booking";
  }

  async function confirmBooking() {
    if (!state.selectedSession || !state.selectedSlot) {
      return;
    }

    const name = els.inputName.value.trim();
    const email = els.inputEmail.value.trim();
    const message = els.inputMessage.value.trim();

    if (!name || !email) {
      els.formError.innerHTML =
        '<div class="error-msg">Please fill in your name and email address.</div>';
      return;
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      els.formError.innerHTML =
        '<div class="error-msg">Please enter a valid email address.</div>';
      return;
    }

    els.formError.innerHTML = "";
    els.confirmButton.disabled = true;
    els.confirmButton.textContent = "Booking...";

    try {
      const response = await fetch(`${API_BASE}/bookings`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          sessionTypeId: state.selectedSession.id,
          startTimeUtc: state.selectedSlot,
          attendeeName: name,
          attendeeEmail: email,
          message: message || null
        })
      });

      const data = await response.json();
      if (!response.ok || !data.success) {
        const errDiv = document.createElement("div");
        errDiv.className = "error-msg";
        errDiv.textContent = data.error || "Something went wrong. Please try again.";
        els.formError.replaceChildren(errDiv);
        resetConfirmButton();
        return;
      }

      const start = new Date(state.selectedSlot);
      const nameBold = document.createElement("strong");
      nameBold.textContent = name;
      const sessionBold = document.createElement("strong");
      sessionBold.textContent = state.selectedSession.name;
      const timeBold = document.createElement("strong");
      timeBold.textContent = formatDateTime(start, true);
      const emailBold = document.createElement("strong");
      emailBold.textContent = email;
      els.confirmText.replaceChildren(
        nameBold,
        document.createTextNode(", your "),
        sessionBold,
        document.createTextNode(" is confirmed for"),
        document.createElement("br"),
        timeBold,
        document.createTextNode("."),
        document.createElement("br"),
        document.createElement("br"),
        document.createTextNode("A calendar invite has been sent to "),
        emailBold,
        document.createTextNode(".")
      );

      if (data.teamsLink) {
        els.teamsLinkButton.href = data.teamsLink;
        els.teamsLinkButton.style.display = "inline-block";
      }

      goTo(4);
    } catch {
      els.formError.innerHTML =
        '<div class="error-msg">Could not connect to the booking service. Please try again.</div>';
      resetConfirmButton();
    }
  }

  function wireEvents() {
    els.sessionList.addEventListener("click", (event) => {
      const card = event.target.closest(".session-card");
      if (!card) {
        return;
      }

      selectSession(card.dataset.id);
    });

    els.slotsContainer.addEventListener("click", (event) => {
      const button = event.target.closest(".slot-btn");
      if (!button) {
        return;
      }

      selectSlot(button.dataset.slot);
    });

    els.chooseTimeButton.addEventListener("click", () => goTo(2));
    els.continueButton.addEventListener("click", () => goTo(3));
    els.stepOneBackButton.addEventListener("click", () => goTo(1));
    els.stepTwoBackButton.addEventListener("click", () => goTo(2));
    els.confirmButton.addEventListener("click", confirmBooking);
    els.prevWeekButton.addEventListener("click", () => {
      if (state.weekOffset <= 0) {
        return;
      }

      state.weekOffset -= 1;
      renderSlots();
    });
    els.nextWeekButton.addEventListener("click", () => {
      state.weekOffset += 1;
      renderSlots();
    });
  }

  setTimeZoneHint();
  wireEvents();
  loadSessions();
})();
