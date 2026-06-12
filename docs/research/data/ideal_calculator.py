"""
Python replication of the 7 C# SubScoreCalculators + AntiFlagEvaluator + composite
assembly from Infrastructure/RelevancePipeline/V2/Scoring/.
"""

import re
from typing import Any, Dict, Iterable, List, Set, Tuple

VERSION_RE = re.compile(r'^(.+?)\s+\d+(\.\d+)*$')

ALIASES = {
    'asp.net': '.net', 'asp.net core': '.net', 'asp.net mvc': '.net',
    'asp.net web api': '.net', '.net core': '.net', '.net framework': '.net',
    'ef core': 'entity framework', 'entity framework core': 'entity framework',
    'google cloud': 'gcp', 'google cloud platform': 'gcp',
    'amazon web services': 'aws', 'microsoft azure': 'azure',
    'k8s': 'kubernetes',
    'reactjs': 'react', 'react.js': 'react',
    'nextjs': 'next.js',
    'nodejs': 'node.js', 'node': 'node.js',
    'vuejs': 'vue', 'vue.js': 'vue',
    'postgres': 'postgresql',
    'mssql': 'sql server', 'microsoft sql server': 'sql server',
    'mongo': 'mongodb',
    'pytorch lightning': 'pytorch', 'tf': 'tensorflow',
    'swiftui': 'swift', 'jetpack compose': 'kotlin',
}


def strip_version(s):
    m = VERSION_RE.match(s)
    return m.group(1).strip() if m else s


def expand_one(raw):
    s = (raw or '').strip()
    if not s:
        return
    yield s.lower()
    stripped = strip_version(s)
    changed = stripped.lower() != s.lower()
    if changed:
        yield stripped.lower()
    a = ALIASES.get(s.lower())
    if a is not None:
        yield a.lower()
    if changed:
        a2 = ALIASES.get(stripped.lower())
        if a2 is not None:
            yield a2.lower()


def expand_all(skills):
    return {v for s in skills for v in expand_one(s)}


def matches(needed, have_expanded):
    return any(v in have_expanded for v in expand_one(needed))


def skill_match(cv, vac):
    must = [s for s in (vac.get('must_have_skills') or []) if isinstance(s, str) and s.strip()]
    nice = [s for s in (vac.get('nice_to_have_skills') or []) if isinstance(s, str) and s.strip()]
    cv_skills = set()
    for fld in ('technical_skills', 'domain_skills'):
        for s in (cv.get(fld) or []):
            if isinstance(s, str) and s.strip():
                cv_skills.add(s)
    cv_exp = expand_all(cv_skills)
    matched_must = sum(1 for m in must if matches(m, cv_exp))
    matched_nice = sum(1 for n in nice if matches(n, cv_exp))
    base = 1.0 if len(must) == 0 else matched_must / len(must)
    bonus = 0.0 if len(nice) == 0 else 0.3 * matched_nice / len(nice)
    return min(1.0, base + bonus)


_SEN_TABLE = {
    ('junior','junior'):1.0,('middle','junior'):0.7,('senior','junior'):0.3,('lead','junior'):0.1,('intern','junior'):0.5,
    ('junior','middle'):0.7,('middle','middle'):1.0,('senior','middle'):0.7,('lead','middle'):0.3,('intern','middle'):0.3,
    ('junior','senior'):0.3,('middle','senior'):0.7,('senior','senior'):1.0,('lead','senior'):0.7,('intern','senior'):0.1,
    ('junior','lead'):0.1,('middle','lead'):0.3,('senior','lead'):0.7,('lead','lead'):1.0,('intern','lead'):0.0,
    ('junior','intern'):0.5,('middle','intern'):0.3,('senior','intern'):0.1,('lead','intern'):0.0,('intern','intern'):1.0,
}


def seniority_match(cv, vac):
    req = (vac.get('seniority_required') or '').lower().strip() or None
    have = (cv.get('seniority') or '').lower().strip() or None
    if req is None or have is None:
        return 0.7
    if req == 'not_specified' or have == 'not_specified':
        return 0.7
    return _SEN_TABLE.get((req, have), 0.3)


def experience_match(cv, vac):
    req_years = vac.get('min_years_experience')
    try:
        req_years = int(req_years) if req_years is not None else 0
    except (TypeError, ValueError):
        req_years = 0
    req_months = req_years * 12
    if req_months <= 0:
        return 1.0
    prod = 0
    for exp in (cv.get('experience') or []):
        if not isinstance(exp, dict): continue
        if exp.get('type') not in ('PRODUCTION', 'FREELANCE'): continue
        d = exp.get('duration_months')
        if isinstance(d, (int, float)):
            prod += int(d)
    if prod >= req_months: return 1.0
    ratio = prod / req_months
    return max(0.5, ratio) if cv.get('career_switcher') is True else ratio


_LADDER = {'a1':1,'a2':2,'b1':3,'b2':4,'c1':5,'c2':6,'native':7,'not_specified':3}


def language_match(cv, vac):
    req = (vac.get('english_required') or 'not_specified').lower()
    have = (cv.get('english_level') or 'not_specified').lower()
    req_i = _LADDER.get(req, 3)
    have_i = _LADDER.get(have, 3)
    delta = have_i - req_i
    if delta >= 0: return 1.0
    if delta == -1: return 0.7
    if delta == -2: return 0.4
    return 0.1


_RANK = {'none':0,'bachelor':1,'associate':1,'master':2,'phd':3,'not_specified':0}


def education_match(cv, vac):
    req = (vac.get('education_required') or 'not_specified').lower()
    req_rank = _RANK.get(req, 0)
    cv_degree = 'none'
    is_relevant = True
    edu = cv.get('education')
    if isinstance(edu, dict):
        cv_degree = (edu.get('degree') or 'none').lower()
        is_relevant = edu.get('is_relevant') is not False
    have_rank = _RANK.get(cv_degree, 0)
    if have_rank >= req_rank:
        base = 1.0
    elif req_rank > 0:
        base = 0.5 + 0.5 * (have_rank / req_rank)
    else:
        base = 1.0
    return base if is_relevant else base * 0.85


_SEN_TOK = {'junior','jr','middle','mid','senior','sr','lead','principal','staff','intern','trainee','strong','head','chief','молодший','старший','провідний','стажер'}
_SUF_TOK = {'engineer','інженер','developer','розробник','specialist','спеціаліст'}


def _role_norm(s):
    lower = s.lower()
    cs = []
    for c in lower:
        cs.append(c if (c.isalnum() or c in ('.', '#', '+')) else ' ')
    parts = ''.join(cs).split()
    return {p for p in parts if len(p)>=2 and p not in _SEN_TOK and p not in _SUF_TOK}


def _jac(a, b):
    if not a or not b: return 0.0
    return len(a & b) / len(a | b)


def role_intent_match(cv, vac):
    rt = vac.get('role_title')
    title = ''
    if isinstance(rt, dict):
        title = rt.get('en') or ''
    vt = _role_norm(title)
    if not vt: return 0.3
    best = 0.0
    for t in (cv.get('target_roles') or []):
        if not isinstance(t, str): continue
        ct = _role_norm(t)
        if not ct: continue
        j = _jac(vt, ct)
        if j > best: best = j
    if best >= 0.66: return 1.0
    if best >= 0.33: return 0.85
    if best > 0: return 0.6
    return 0.3


def _tokens_dom(s):
    lower = s.lower()
    cs = [c if (c.isalnum() or c in ('.', '#', '+')) else ' ' for c in lower]
    return [t for t in ''.join(cs).split() if len(t) >= 2]


def domain_alignment(cv, vac):
    domain_en = ''
    dc = vac.get('domain_context')
    if isinstance(dc, dict):
        domain_en = dc.get('en') or ''
    if not domain_en.strip() or domain_en.strip().lower() == 'other':
        return 0.7
    dom = set(_tokens_dom(domain_en))
    if not dom: return 0.7
    cv_dom = set()
    for s in (cv.get('domain_skills') or []):
        if isinstance(s, str):
            cv_dom.update(_tokens_dom(s))
    overlap = len(dom & cv_dom)
    return min(1.0, 0.5 + 0.5 * overlap / len(dom))


_FOREIGN = {'french','german','spanish','italian','polish','dutch','japanese','chinese','arabic'}


def _is_foreign(f):
    for l in _FOREIGN:
        if l in f: return True, l
    return False, ''


def _cv_has_lang(cv, lang):
    for L in (cv.get('languages') or []):
        if isinstance(L, dict) and lang in (L.get('language') or '').lower():
            return True
    return False


def anti_flag_evaluator(cv, vac):
    anti = [s for s in (vac.get('anti_requirements') or []) if isinstance(s, str) and s.strip()]
    triggered = []
    for f in anti:
        fl = f.lower()
        is_l, lang = _is_foreign(fl)
        if is_l:
            if not _cv_has_lang(cv, lang):
                triggered.append(f)
            continue
        triggered.append(f)
    n = len(triggered)
    return (1.0 if n == 0 else (0.5 if n == 1 else 0.2)), triggered


WEIGHTS = {'skill_match':0.30,'seniority_match':0.15,'experience_match':0.15,
           'language_match':0.10,'education_match':0.05,'role_intent_match':0.15,
           'domain_alignment':0.10}


def compute_all(cv, vac):
    ss = {
        'skill_match':       max(0.0, min(1.0, skill_match(cv, vac))),
        'seniority_match':   max(0.0, min(1.0, seniority_match(cv, vac))),
        'experience_match':  max(0.0, min(1.0, experience_match(cv, vac))),
        'language_match':    max(0.0, min(1.0, language_match(cv, vac))),
        'education_match':   max(0.0, min(1.0, education_match(cv, vac))),
        'role_intent_match': max(0.0, min(1.0, role_intent_match(cv, vac))),
        'domain_alignment':  max(0.0, min(1.0, domain_alignment(cv, vac))),
    }
    penalty, triggered = anti_flag_evaluator(cv, vac)
    weighted = sum(ss[k] * w for k, w in WEIGHTS.items())
    score = max(0.0, min(1.0, weighted * penalty))
    return {'sub_scores': ss, 'anti_flag_penalty': penalty,
            'triggered_anti_flags': triggered, 'score': score}


def verdict_from_score(s):
    if s >= 0.75: return 'Strong'
    if s >= 0.50: return 'Partial'
    if s >= 0.25: return 'Weak'
    return 'Mismatch'
