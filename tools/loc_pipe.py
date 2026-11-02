# 글자 테이블 다듬기 · 번역 · 검수 (agy CLI)
#   python -I tools/loc_pipe.py polish            한국어 다듬기 (제미나이) → Tables/ko 덮어씀
#   python -I tools/loc_pipe.py translate en ja … 번역 (클로드) → Tables/<언어>
#   python -I tools/loc_pipe.py review en ja …    그 언어 원어민 관점 검수 (제미나이) → Tables/<언어> 덮어씀
# 키 · {0} 자리 · 태그가 원문과 다르면 다시 시키고, 그래도 틀리면 그 묶음은 건너뜀 (게임은 한국어로 대체해 보여 줌)
import json, os, re, subprocess, sys, tempfile, threading
from concurrent.futures import ThreadPoolExecutor

sys.stdout.reconfigure(encoding="utf-8")
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TABLES = os.path.join(ROOT, "RatWarUnity", "Assets", "Resources", "Tables")
WORK = os.path.join(tempfile.gettempdir(), "ratwar_loc")
GEMINI, CLAUDE = os.environ.get("LOC_GEMINI", "gemini-4-argon-high"), "claude-sonnet-5-5-high"  # 할당량이 다 되면 LOC_GEMINI=gemini-3.8-flash-high
CHUNK, JOBS = 40, int(os.environ.get("LOC_JOBS", "8"))
LANG = {"en": "English", "ja": "Japanese", "zh": "Simplified Chinese", "ru": "Russian", "fr": "French", "de": "German", "es": "Spanish (Spain)"}
JOSA = "은|는|이|가|을|를|과|와|으로|로|이라|라|아|야"
lock = threading.Lock()

GAME = """GAME CONTEXT: "Band of Mice" — a turn-based tactics game (XCOM-like) where a squad of rat soldiers of the Republic of Ratteria fights the cat soldiers of the Felinian Empire in a fictional WW2-style war. Each rat is a person with a name, hometown, backstory, traits and stress; when they die they go to a memorial. Tone: gritty, warm, a little dark humor, never cute. Story, logs and epilogue lines are terse plain narration (like Band of Brothers end cards). UI labels and buttons are short."""

RULES = """HARD RULES:
- Keep every key exactly. Output a JSON object with the same keys, nothing else.
- Keep placeholders like {0}, {1}, {0:0.#} exactly (same numbers, same count). Korean particle slots like {0:은} {1:으로} {0:이라} are Korean-only: in other languages write them as plain {0}, {1}.
- Keep rich-text tags (<b>, </b>, <i>, </i>, <size=..>, </size>, <color=..>, </color>) exactly, same count. Keep \\n line breaks where they are.
- Keep numbers, + / − signs, % and symbols like · ✓ ⏎ ● ○ ■ ◆.
- Do not add explanations, hints or information that is not in the source. Never reveal hidden class unlock recipes.
- UI labels / button texts (keys containing .btn, .tab, .label, scene.*, short values) must stay about as short as the source."""


def load(lang):
    out = {}
    d = os.path.join(TABLES, lang)
    if not os.path.isdir(d):
        return out
    for f in sorted(os.listdir(d)):
        if f.endswith(".json"):
            out[f] = json.load(open(os.path.join(d, f), encoding="utf-8"))
    return out


def save(lang, name, table):
    d = os.path.join(TABLES, lang)
    os.makedirs(d, exist_ok=True)
    with open(os.path.join(d, name), "w", encoding="utf-8", newline="\n") as f:
        json.dump(table, f, ensure_ascii=False, indent=2)
        f.write("\n")


def slots(s, keep_josa):
    out = []
    for m in re.finditer(r"\{(\d+)(?::([^}]+))?\}", s):
        spec = m.group(2)
        if spec and re.fullmatch(JOSA, spec) and not keep_josa:
            spec = None
        out.append((m.group(1), spec))
    return sorted(out, key=str)


def tags(s):
    return sorted(re.findall(r"</?(?:b|i|size|color)\b", s))


def strip_josa(s):
    return re.sub(r"\{(\d+):(?:%s)\}" % JOSA, r"{\1}", s)


def check(src, dst, ko_target):
    errs = []
    if set(src) != set(dst):
        errs.append("keys differ: missing %s extra %s" % (sorted(set(src) - set(dst))[:5], sorted(set(dst) - set(src))[:5]))
    for k in src:
        if k not in dst:
            continue
        if not isinstance(dst[k], str):
            errs.append(k + ": not a string")
            continue
        if slots(src[k], ko_target) != slots(dst[k], ko_target):
            errs.append("%s: placeholders %s → %s" % (k, slots(src[k], ko_target), slots(dst[k], ko_target)))
        if tags(src[k]) != tags(dst[k]):
            errs.append(k + ": rich-text tags changed")
    return errs


def agy(model, workdir, prompt):
    r = subprocess.run(["agy", "--model", model, "--mode", "accept-edits", "--print-timeout", "600s", "-p=" + prompt],
                       cwd=workdir, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=900)
    return r.stdout


def run_chunk(tag, model, src, prompt, extra_files, ko_target):
    wd = os.path.join(WORK, tag)
    os.makedirs(wd, exist_ok=True)
    for n, obj in extra_files.items():
        json.dump(obj, open(os.path.join(wd, n), "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    out = os.path.join(wd, "out.json")
    note, errs = "", []
    for attempt in range(3):
        if os.path.exists(out):
            os.remove(out)
        # 파일 쓰기 대신 글로 받음 (도구 권한 없이): 입력 파일을 프롬프트에 붙이고 출력된 JSON 을 out.json 으로
        inline = "\n\nFILES (inline — you have no file access, do not use tools):\n" + "".join(
            "=== %s ===\n%s\n" % (n, json.dumps(obj, ensure_ascii=False, indent=1)) for n, obj in extra_files.items())
        inline += "\nOUTPUT: print ONLY the resulting JSON object (what out.json would contain). No code fences, no other text."
        text = agy(model, wd, prompt + note + inline)
        try:
            body = text[text.index("{"):text.rindex("}") + 1]
            dst = json.loads(body)
            json.dump(dst, open(out, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
        except Exception as e:
            note = "\n\nPREVIOUS ATTEMPT FAILED: out.json was missing or not valid JSON (%s). Write valid JSON to out.json." % e
            continue
        if not ko_target:
            dst = {k: strip_josa(v) if isinstance(v, str) else v for k, v in dst.items()}
        errs = check(src, dst, ko_target)
        if not errs:
            with lock:
                print("ok  ", tag, flush=True)
            return dst
        note = "\n\nPREVIOUS ATTEMPT HAD ERRORS, fix them:\n" + "\n".join(errs[:20])
    if os.path.exists(out):
        os.remove(out)  # 실패한 묶음은 다음에 다시
    with lock:
        print("FAIL", tag, errs[:3] or note[:200], flush=True)
    return None


def chunks(table):
    items = list(table.items())
    for i in range(0, len(items), CHUNK):
        yield i // CHUNK, dict(items[i:i + CHUNK])


def polish():
    ko = load("ko")
    jobs = []
    prompt = GAME + "\n\n" + RULES + """

TASK: You are a native Korean game localization editor. in.json holds Korean UI/story strings of this game.
Rewrite each value so it reads naturally to a Korean gamer: fix stiff, translated-sounding or "AI-like" phrasing (over-formal endings, needless em-dashes, robotic noun-chains, awkward 피동/번역투), keep the meaning, numbers and tone. Keep 반말 narration for stories/logs and the existing politeness level for system messages. Keep names and proper nouns as they are. If a value is already natural, leave it unchanged.
Keep Korean particle slots like {0:은} exactly as they are.
Write the result to out.json (same keys). Then reply DONE."""
    with ThreadPoolExecutor(JOBS) as ex:
        for name, table in ko.items():
            for i, part in chunks(table):
                tag = "ko_%s_%d" % (name[:-5], i)
                jobs.append((name, ex.submit(run_chunk, tag, GEMINI, part, prompt, {"in.json": part}, True)))
    changed = 0
    for name, fut in jobs:
        res = fut.result()
        if res:
            for k, v in res.items():
                if ko[name][k] != v:
                    changed += 1
                ko[name][k] = v
    for name, table in ko.items():
        save("ko", name, table)
    print("polish done, changed", changed)


GLOSS = re.compile(r"^(camp\.(name|home|rank|fur)\.|camp\.trait\.[a-z]+\.name$|game\.(class|weapon|sector)\.|game\.abil\.[a-z_]+\.name$|campaign\.(hex|kind|special\.[a-z0-9]+\.name|fx\.)|camp\.stress\.)")


def glossary(lang, ko):
    path = os.path.join(WORK, "gloss_%s.json" % lang)
    if os.path.exists(path):
        return json.load(open(path, encoding="utf-8"))
    src = {k: v for t in ko.values() for k, v in t.items() if GLOSS.match(k)}
    src["title"] = "Band of Mice"
    prompt = GAME + "\n\n" + RULES + """

TASK: Translate the Korean glossary in in.json (names of soldiers, hometowns, army ranks, traits, classes, weapons, abilities, map places, factions) into %s for this game. This glossary will be reused for every other string, so choose the natural, consistent rendering a professional %s game localizer would pick:
- Rat soldier names are European first names written in Hangul (쥬드 Jude, 미르코 Mirko, 파벨 Pavel, 코스챠 Kostya, 루드비크 Ludwik, 조피아 Zofia …): write them the way that name is normally spelled in %s.
- Ranks: use that language's real army enlisted/NCO ranks in ascending order.
- Factions: 라테리아 공화국 = Republic of Ratteria, 펠리니아 제국 = Felinian Empire, 펠린부르크 = Felinburg (adapt spelling to the language). The game title "Band of Mice" stays in English in every language.
Write out.json with the same keys. Then reply DONE.""" % (LANG[lang], LANG[lang], LANG[lang])
    res = run_chunk("gloss_" + lang, CLAUDE, src, prompt, {"in.json": src}, False)
    if res:
        json.dump(res, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    return res or {}


def translate(langs):
    ko = load("ko")
    with ThreadPoolExecutor(len(langs)) as ex:
        glos = dict(zip(langs, ex.map(lambda l: glossary(l, ko), langs)))
    jobs = []
    with ThreadPoolExecutor(JOBS) as ex:
        for lang in langs:
            pairs = {k: {"ko": v, LANG[lang]: glos[lang].get(k, "")} for t in ko.values() for k, v in t.items() if k in glos[lang]}
            prompt = GAME + "\n\n" + RULES + """

TASK: Translate the Korean strings in in.json into natural %s for this game, as a professional game localizer writing for %s-speaking players (not a literal translation). glossary.json gives the fixed renderings of names, places, ranks, traits, classes and abilities — always use them. Keys tell you where the text appears (ui.* menus, hud.* battle HUD, battle.* combat log/popups, camp.* squad & traits, war.*/campaign.* strategic map and cutscenes, epi.* epilogue end cards, scene.* fixed labels).
Write out.json with the same keys. Then reply DONE.""" % (LANG[lang], LANG[lang])
            for name, table in ko.items():
                for i, part in chunks(table):
                    tag = "%s_%s_%d" % (lang, name[:-5], i)
                    jobs.append((lang, name, ex.submit(run_chunk, tag, CLAUDE, part, prompt, {"in.json": part, "glossary.json": pairs}, False)))
    write(jobs, langs, ko)


def review(langs):
    ko = load("ko")
    jobs = []
    with ThreadPoolExecutor(JOBS) as ex:
        for lang in langs:
            cur = load(lang)
            prompt = GAME + "\n\n" + RULES + """

TASK: You are a native %s speaker and senior game localization reviewer. in.json is the Korean source, tr.json is the current %s translation (same keys). Edit the translation so every line reads naturally and idiomatically to a %s-speaking player — fix literal/awkward phrasing, wrong register, grammar, inconsistent terms, overly long UI labels, and meaning errors versus the Korean. Keep names, places and game terms consistent with how tr.json already renders them unless clearly wrong. If a line is already good, keep it.
Write the corrected full translation to out.json (same keys). Then reply DONE.""" % (LANG[lang], LANG[lang], LANG[lang])
            for name, table in ko.items():
                tr = cur.get(name, {})
                for i, part in chunks(table):
                    trp = {k: tr.get(k, "") for k in part}
                    tag = "rv_%s_%s_%d" % (lang, name[:-5], i)
                    if os.path.exists(os.path.join(WORK, tag, "out.json")):
                        continue  # 이미 검수한 묶음
                    jobs.append((lang, name, ex.submit(run_chunk, tag, GEMINI, part, prompt, {"in.json": part, "tr.json": trp}, False)))
    write(jobs, langs, ko)


def write(jobs, langs, ko):
    out = {l: load(l) for l in langs}
    for lang, name, fut in jobs:
        res = fut.result()
        if res:
            out[lang].setdefault(name, {}).update(res)
    for lang in langs:
        for name in ko:
            t = out[lang].get(name, {})
            save(lang, name, {k: t[k] for k in ko[name] if k in t})
        n = sum(len(t) for t in out[lang].values())
        print(lang, "keys", n, "/", sum(len(t) for t in ko.values()))


def check_lang(langs):
    ko = load("ko")
    for lang in langs:
        cur, bad = load(lang), 0
        for name, table in ko.items():
            tr = cur.get(name, {})
            miss = [k for k in table if k not in tr]
            if miss:
                bad += len(miss)
                print(lang, name, "missing", len(miss), miss[:5])
            errs = check({k: v for k, v in table.items() if k in tr}, {k: strip_josa(v) for k, v in tr.items() if k in table}, False)
            for e in errs:
                bad += 1
                print(lang, name, e)
        print(lang, "OK" if bad == 0 else "%d problems" % bad)


if __name__ == "__main__":
    os.makedirs(WORK, exist_ok=True)
    mode, langs = sys.argv[1], sys.argv[2:] or list(LANG)
    {"polish": lambda: polish(), "translate": lambda: translate(langs), "review": lambda: review(langs), "check": lambda: check_lang(langs)}[mode]()
