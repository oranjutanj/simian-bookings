// Of COURSE this is vibe coded. No judging.

(() => {
  const configuredApiBase = (window.SIMIAN_CONFIG?.apiBase || "").trim();
  const defaultProdApiBase = "https://simian-bookings-api-d8hgfzhxa8fjbwfv.ukwest-01.azurewebsites.net/api";

  function normalizeApiBase(rawValue) {
    if (!rawValue) {
      return "";
    }

    let normalized = rawValue.trim();
    if (!/^https?:\/\//i.test(normalized)) {
      normalized = `https://${normalized}`;
    }

    normalized = normalized.replace(/\/+$/, "");
    if (!/\/api$/i.test(normalized)) {
      normalized = `${normalized}/api`;
    }

    return normalized;
  }

  const API_BASE =
    configuredApiBase
      ? normalizeApiBase(configuredApiBase)
      : window.location.protocol === "file:" ||
        window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1"
      ? "http://localhost:7071/api"
      : defaultProdApiBase;

  const state = {
    sessions: [],
    selectedSession: null,
    selectedSlot: null,
    // Calendar state
    viewYear: null,
    viewMonth: null,    // 0-indexed
    selectedDate: null, // "YYYY-MM-DD"
    monthSlots: {},     // { "YYYY-MM-DD": ["ISO string", ...] }
    userTimeZone: Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC"
  };

  const els = {
    sessionList: document.getElementById("session-list"),
    chooseTimeButton: document.getElementById("btn-to-step-2"),
    continueButton: document.getElementById("btn-to-step-3"),
    // Calendar elements
    prevMonthButton: document.getElementById("btn-prev-month"),
    nextMonthButton: document.getElementById("btn-next-month"),
    monthLabel: document.getElementById("month-label"),
    calendarLoading: document.getElementById("calendar-loading"),
    calendarGrid: document.getElementById("calendar-grid"),
    timeSlotsPlaceholder: document.getElementById("time-slots-placeholder"),
    timeSlotsContent: document.getElementById("time-slots-content"),
    selectedDayHeading: document.getElementById("selected-day-heading"),
    timeSlotsList: document.getElementById("time-slots-list"),
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

  function toDateKey(date) {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, "0");
    const d = String(date.getDate()).padStart(2, "0");
    return `${y}-${m}-${d}`;
  }

  function getTodayKey() {
    return toDateKey(new Date());
  }

  function getMonthRange(year, month) {
    const from = new Date(Date.UTC(year, month, 1));
    const to = new Date(Date.UTC(year, month + 1, 1));
    return { from, to };
  }

  function showCalendarLoading(visible) {
    els.calendarLoading.style.display = visible ? "flex" : "none";
    els.calendarGrid.style.display = visible ? "none" : "grid";
  }

  function showTimeSlotsContent(visible) {
    els.timeSlotsPlaceholder.style.display = visible ? "none" : "block";
    els.timeSlotsContent.style.display = visible ? "block" : "none";
  }

  function renderCalendar() {
    const { viewYear: year, viewMonth: month } = state;
    const now = new Date();
    const todayKey = getTodayKey();

    // Update month/year label
    const label = new Date(year, month, 1).toLocaleDateString(undefined, {
      month: "long",
      year: "numeric"
    });
    els.monthLabel.textContent = label;

    // Disable prev button if we're already at the current month
    const isCurrentMonth =
      year === now.getFullYear() && month === now.getMonth();
    els.prevMonthButton.disabled = isCurrentMonth;

    // Day-of-week headers (Monday-first)
    const dowHeaders = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    let html = dowHeaders.map((d) => `<div class="cal-dow">${d}</div>`).join("");

    // Padding cells before first day (Monday-based: Mon=0, Sun=6)
    const firstDow = (new Date(year, month, 1).getDay() + 6) % 7;
    for (let i = 0; i < firstDow; i++) {
      html += '<div class="cal-day other-month"></div>';
    }

    const daysInMonth = new Date(year, month + 1, 0).getDate();

    for (let d = 1; d <= daysInMonth; d++) {
      const dateKey = `${year}-${String(month + 1).padStart(2, "0")}-${String(d).padStart(2, "0")}`;
      const hasSlots = !!(state.monthSlots[dateKey]?.length);
      const isToday = dateKey === todayKey;
      const isSelected = dateKey === state.selectedDate;
      const isPast = dateKey < todayKey;

      let classes = "cal-day";
      if (isPast) classes += " no-slots";
      else if (hasSlots) classes += " has-slots";
      if (isToday) classes += " today";
      if (isSelected) classes += " selected-day";

      const clickable = hasSlots && !isPast;
      html += `<button type="button" class="${classes}"${clickable ? ` data-date="${escapeHtml(dateKey)}"` : " disabled"}>${d}</button>`;
    }

    els.calendarGrid.innerHTML = html;
  }

  function renderDaySlots(dateKey) {
    const slots = state.monthSlots[dateKey] ?? [];
    if (!slots.length) return;

    state.selectedDate = dateKey;
    state.selectedSlot = null;
    els.continueButton.disabled = true;

    // Update heading: "Wednesday, 11 June"
    const dateObj = new Date(dateKey + "T00:00:00");
    els.selectedDayHeading.textContent = dateObj.toLocaleDateString(undefined, {
      weekday: "long",
      day: "numeric",
      month: "long"
    });

    els.timeSlotsList.innerHTML = slots
      .map(
        (slot) =>
          `<button type="button" class="slot-btn" data-slot="${escapeHtml(slot)}">${escapeHtml(formatTime(new Date(slot)))}</button>`
      )
      .join("");

    showTimeSlotsContent(true);

    // Re-render calendar to update selected-day highlight
    renderCalendar();
  }

  function selectSlot(slotUtc) {
    state.selectedSlot = slotUtc;
    document.querySelectorAll(".slot-btn").forEach((button) => {
      button.classList.toggle("selected", button.dataset.slot === slotUtc);
    });
    els.continueButton.disabled = false;
  }

  async function loadSlotsForMonth(year, month) {
    if (!state.selectedSession) return;

    state.viewYear = year;
    state.viewMonth = month;
    state.selectedDate = null;
    state.selectedSlot = null;
    state.monthSlots = {};
    els.continueButton.disabled = true;

    showCalendarLoading(true);
    showTimeSlotsContent(false);

    const { from, to } = getMonthRange(year, month);

    try {
      const url = `${API_BASE}/slots?sessionType=${encodeURIComponent(state.selectedSession.id)}&from=${encodeURIComponent(from.toISOString())}&to=${encodeURIComponent(to.toISOString())}`;
      const response = await fetch(url);

      if (!response.ok) {
        const body = await response.text();
        throw new Error(`Slots request failed (${response.status}). ${body.slice(0, 180)}`);
      }

      const data = await response.json();
      const slots = data.availableSlots ?? [];

      // Group by local date key
      const grouped = {};
      for (const slot of slots) {
        const key = toDateKey(new Date(slot));
        if (!grouped[key]) grouped[key] = [];
        grouped[key].push(slot);
      }
      state.monthSlots = grouped;

      showCalendarLoading(false);
      renderCalendar();
    } catch (error) {
      console.error("Failed to load slots for month", {
        apiBase: API_BASE,
        sessionType: state.selectedSession?.id,
        year,
        month,
        error
      });
      showCalendarLoading(false);
      els.calendarGrid.innerHTML =
        '<div class="error-msg" style="grid-column:1/-1">Could not load availability. Please try again.</div>';
    }
  }

  async function loadSessions() {
    try {
      const response = await fetch(`${API_BASE}/session-types`);
      if (!response.ok) {
        const body = await response.text();
        throw new Error(`Session request failed (${response.status}). ${body.slice(0, 180)}`);
      }

      state.sessions = await response.json();
      renderSessions();
    } catch (error) {
      console.error("Failed to load sessions", { apiBase: API_BASE, error });
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
      const heading = document.getElementById('step-2-heading');
      if (heading && state.selectedSession) {
        heading.textContent = state.selectedSession.name;
      }
      const now = new Date();
      loadSlotsForMonth(now.getFullYear(), now.getMonth());
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
    } catch (error) {
      console.error("Booking request failed", {
        apiBase: API_BASE,
        sessionType: state.selectedSession?.id,
        slot: state.selectedSlot,
        error
      });
      els.formError.innerHTML =
        '<div class="error-msg">Could not connect to the booking service. Please try again.</div>';
      resetConfirmButton();
    }
  }

  function wireEvents() {
    els.sessionList.addEventListener("click", (event) => {
      const card = event.target.closest(".session-card");
      if (!card) return;
      selectSession(card.dataset.id);
    });

    // Calendar day clicks
    els.calendarGrid.addEventListener("click", (event) => {
      const day = event.target.closest(".cal-day[data-date]");
      if (!day) return;
      renderDaySlots(day.dataset.date);
    });

    // Time slot clicks
    document.getElementById("time-slots-list").addEventListener("click", (event) => {
      const button = event.target.closest(".slot-btn");
      if (!button) return;
      selectSlot(button.dataset.slot);
    });

    els.prevMonthButton.addEventListener("click", () => {
      let { viewYear, viewMonth } = state;
      viewMonth -= 1;
      if (viewMonth < 0) { viewMonth = 11; viewYear -= 1; }
      loadSlotsForMonth(viewYear, viewMonth);
    });

    els.nextMonthButton.addEventListener("click", () => {
      let { viewYear, viewMonth } = state;
      viewMonth += 1;
      if (viewMonth > 11) { viewMonth = 0; viewYear += 1; }
      loadSlotsForMonth(viewYear, viewMonth);
    });

    els.chooseTimeButton.addEventListener("click", () => goTo(2));
    els.continueButton.addEventListener("click", () => goTo(3));
    els.stepOneBackButton.addEventListener("click", () => goTo(1));
    els.stepTwoBackButton.addEventListener("click", () => goTo(2));
    els.confirmButton.addEventListener("click", confirmBooking);
  }

  setTimeZoneHint();
  wireEvents();
  loadSessions();
})();
