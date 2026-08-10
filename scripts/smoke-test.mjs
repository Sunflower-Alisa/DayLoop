#!/usr/bin/env node
/* DayLoop 真实服务器冒烟测试
 * 用法: node scripts/smoke-test.mjs [baseUrl] [username]
 * 默认 baseUrl = http://localhost:5000
 * 直接对运行中的后端+前端做端到端验证。
 */
const base = process.argv[2] || "http://localhost:5000";
const user = process.argv[3] || `smoke_${Date.now().toString(36)}`;
const PASS = "SmokeTest!2026";

let passed = 0, failed = 0;
const results = [];

function check(name, ok, info = "") {
  if (ok) { passed++; } else { failed++; }
  results.push({ name, ok, info });
  console.log(`${ok ? "PASS" : "FAIL"}  ${name}${info ? "  -> " + info : ""}`);
}

async function req(method, path, { token, body } = {}) {
  const headers = { "Content-Type": "application/json" };
  if (token) headers.Authorization = `Bearer ${token}`;
  if (body === undefined) delete headers["Content-Type"];
  const res = await fetch(base + path, {
    method,
    headers,
    body: body ? JSON.stringify(body) : undefined,
  });
  let data = null;
  try { data = await res.json(); } catch { /* ignore */ }
  return { status: res.status, data };
}
const ok2xx = s => s >= 200 && s < 300;

async function main() {
  console.log(`>>> Smoke test against ${base}  user=${user}\n`);

  // ---- Static frontend ----
  {
    const html = await fetch(base + "/").then(r => r.text()).catch(() => "");
    check("GET / index.html", html.includes("<!DOCTYPE html>") || html.includes("<div id=\"app\">"));
    const jsName = (html.match(/assets\/index-[A-Za-z0-9_-]+\.js/) || [null])[0];
    check("index.html references JS bundle", !!jsName, jsName);
    if (jsName) {
      const r = await fetch(base + "/" + jsName);
      check("GET " + jsName, r.ok, r.status);
    }
    const v = await req("GET", "/api/version");
    check("GET /api/version", v.status === 200 && v.data.version, JSON.stringify(v.data?.version));
  }

  // ---- Auth ----
  let token = null;
  {
    const reg = await req("POST", "/api/auth/register", { body: { username: user, password: PASS } });
    check("POST /api/auth/register", reg.status === 200 || reg.status === 201);
    const login = await req("POST", "/api/auth/login", { body: { username: user, password: PASS } });
    token = login.data?.token || login.data?.accessToken;
    check("POST /api/auth/login -> token", !!token, login.status);
  }

  // ---- Words / English patch ----
  let bookId = 0;
  {
    const books = await req("GET", "/api/words/books", { token });
    const list = Array.isArray(books.data) ? books.data : [];
    bookId = list[0]?.id || 0;
    check("GET /api/words/books (seeded)", list.length > 0, `count=${list.length} word_count=${list[0]?.word_count}`);

    const daily = await req("GET", "/api/words/daily", { token });
    check("GET /api/words/daily", daily.status === 200, `newDone=${daily.data?.new_done}`);

    const words = await req("GET", `/api/words/books/${bookId}`, { token });
    const wordList = words.data?.words || [];
    check(`GET /api/words/books/${bookId}`, wordList.length > 0, `words=${wordList.length}`);

    if (wordList.length > 0) {
      const wid = wordList[0].id;
      const learn = await req("POST", "/api/words/learn", { token, body: { word_id: wid, correct: true, know: true, is_review: false } });
      check("POST /api/words/learn", learn.status === 200, learn.status);
      const wrong = await req("GET", "/api/words/wrong", { token });
      check("GET /api/words/wrong", wrong.status === 200);
    }
  }

  // ---- Scenarios ----
  {
    const sc = await req("GET", "/api/scenarios", { token });
    const list = Array.isArray(sc.data) ? sc.data : [];
    check("GET /api/scenarios (seeded)", list.length > 0, `count=${list.length}`);
    if (list.length > 0) {
      const id = list[0].id;
      const detail = await req("GET", `/api/scenarios/${id}`, { token });
      check("GET /api/scenarios/{id}", detail.status === 200 && !!detail.data?.scenario);
      const quiz = await req("POST", `/api/scenarios/${id}/quiz`, { token, body: { answers: [] } });
      check("POST /api/scenarios/{id}/quiz", quiz.status === 200);
    }
  }

  // ---- Speaking ----
  {
    const topics = await req("GET", "/api/speaking/topics", { token });
    const list = Array.isArray(topics.data) ? topics.data : [];
    check("GET /api/speaking/topics (seeded)", list.length > 0, `count=${list.length}`);
    if (list.length > 0) {
      const rec = await req("POST", "/api/speaking/records", { token, body: { topic_id: list[0].id, audio_url: "", duration: 5 } });
      check("POST /api/speaking/records", rec.status === 200);
    }
  }

  // ---- Clips ----
  {
    const clips = await req("GET", "/api/clips", { token });
    const list = Array.isArray(clips.data) ? clips.data : [];
    check("GET /api/clips (seeded)", list.length > 0, `count=${list.length}`);
  }

  // ---- English dashboard / stats ----
  {
    const dash = await req("GET", "/api/english/dashboard", { token });
    check("GET /api/english/dashboard", dash.status === 200);
    const streak = await req("GET", "/api/english/streak", { token });
    check("GET /api/english/streak", streak.status === 200);
    const sess = await req("POST", "/api/english/sessions", { token, body: { type: "words", minutes: 5 } });
    check("POST /api/english/sessions", sess.status === 200);
  }

  // ---- Core modules ----
  {
const tasks = await req("POST", "/api/tasks", { token, body: { title: "smoke task " + Date.now().toString(36), date: "2026-08-03", category: "other" } });
    const taskId = tasks.data?.id;
    check("POST /api/tasks", ok2xx(tasks.status) && !!taskId, `status=${tasks.status}`);

    const notes = await req("POST", "/api/notes", { token, body: { title: "smoke note", content: "hello" } });
    check("POST /api/notes", ok2xx(notes.status), `status=${notes.status}`);

    const questions = await req("POST", "/api/questions", { token, body: { title: "smoke q", answer: "a", source: "self" } });
    check("POST /api/questions", ok2xx(questions.status), `status=${questions.status}`);

    const rev = await req("PUT", "/api/reviews/2026-08-03", { token, body: { content: "smoke review", tags: "" } });
    check("PUT /api/reviews/{date}", ok2xx(rev.status), `status=${rev.status}`);

    const stats = await req("GET", "/api/stats", { token });
    check("GET /api/stats", stats.status === 200);

    const ach = await req("GET", "/api/achievements", { token });
    check("GET /api/achievements", ach.status === 200);

    const summ = await req("GET", "/api/summaries/list?type=weekly", { token });
    check("GET /api/summaries/list", ok2xx(summ.status), `status=${summ.status}`);

    const exp = await req("GET", "/api/export/json", { token });
    check("GET /api/export/json", exp.status === 200);
  }

  // ---- Cleanup: delete account ----
  {
    const del = await req("DELETE", "/api/auth/account", { token });
    check("DELETE /api/auth/account", del.status === 200);
  }

  console.log(`\n=== ${passed} passed, ${failed} failed ===`);
  process.exit(failed ? 1 : 0);
}

main().catch(e => { console.error("Fatal:", e.message); process.exit(2); });