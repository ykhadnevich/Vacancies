"""
Fair comparison: split vs monolithic against the IDEAL score (calculators on gold data).
Also direct comparisons on clean signals: composite, verdict bucket,
missing_must_haves Jaccard, reason text.
"""
import sys, json, os, glob, statistics as st
sys.path.insert(0, '/tmp/analysis')
import ideal_calculator as ic

BASE = '.'


def load_pairs():
    sel = json.load(open(f'{BASE}/gold_set_v2/vacancies/selected/selected.json'))
    out = []
    for cv_id, pool in sel['cv_pools'].items():
        for s in pool['selected']:
            out.append((cv_id, s['vacancy_id']))
    return out


def load_gold_cvs(cv_ids):
    cache = {}
    for cv_id in cv_ids:
        try:
            cache[cv_id] = json.load(open(f'{BASE}/gold_set/expected/{cv_id}.json'))
        except FileNotFoundError:
            pass
    return cache


def load_json_safe(path):
    try:
        return json.load(open(path))
    except FileNotFoundError:
        return None


def token_jaccard(a, b):
    """Set Jaccard over canonical-expanded skill tokens."""
    ae = ic.expand_all([s for s in a if isinstance(s, str)])
    be = ic.expand_all([s for s in b if isinstance(s, str)])
    if not ae and not be:
        return 1.0
    if not ae or not be:
        return 0.0
    return len(ae & be) / len(ae | be)


def verdict(s):
    return ic.verdict_from_score(s)


def main():
    pairs = load_pairs()
    cvs = load_gold_cvs(set(p[0] for p in pairs))

    rows = []
    for cv_id, vid in pairs:
        gold_vac = load_json_safe(f'{BASE}/gold_set_v2/vacancies/expected/{vid}.json')
        split = load_json_safe(f'{BASE}/results/scoring_20260523_002153/{cv_id}/{vid}.json')
        mono = load_json_safe(f'{BASE}/results/scoring_monolithic_20260523_231542/{cv_id}/{vid}.json')
        cv = cvs.get(cv_id)
        if not all([gold_vac, split, mono, cv]):
            continue

        ideal = ic.compute_all(cv, gold_vac)
        rows.append({
            'cv_id': cv_id, 'vid': vid,
            'ideal': ideal,
            'split': split,
            'mono':  mono,
            'gold_vac': gold_vac,
        })

    n = len(rows)
    print(f'==== Fair comparison on N={n} pairs ====\n')

    # ---------------- (A) Score-distance to ideal --------------------
    split_dist = [abs(r['split']['score'] - r['ideal']['score']) for r in rows]
    mono_dist  = [abs(r['mono']['score']  - r['ideal']['score']) for r in rows]
    split_signed = [r['split']['score'] - r['ideal']['score'] for r in rows]
    mono_signed  = [r['mono']['score']  - r['ideal']['score'] for r in rows]

    print('--- (A) |composite - ideal| ---')
    print(f'  split  mean={st.mean(split_dist):.4f}  median={st.median(split_dist):.4f}  stdev={st.pstdev(split_dist):.4f}')
    print(f'  mono   mean={st.mean(mono_dist):.4f}  median={st.median(mono_dist):.4f}  stdev={st.pstdev(mono_dist):.4f}')
    wins_split = sum(1 for s, m in zip(split_dist, mono_dist) if s < m)
    wins_mono  = sum(1 for s, m in zip(split_dist, mono_dist) if m < s)
    ties = n - wins_split - wins_mono
    print(f'  per-pair wins:  split={wins_split} ({wins_split/n:.1%})  '
          f'mono={wins_mono} ({wins_mono/n:.1%})  ties={ties}')
    print(f'  signed bias (mean score - ideal):  split={st.mean(split_signed):+.4f}  '
          f'mono={st.mean(mono_signed):+.4f}')

    # ---------------- (B) Sub-score-level distance to ideal -----------
    print('\n--- (B) per-axis |sub_score - ideal| (mean) ---')
    print(f'  {"axis":<22}{"split":>10}{"mono":>10}{"winner":>12}')
    for axis in ic.WEIGHTS:
        sd = st.mean(abs(r['split']['sub_scores'][axis] - r['ideal']['sub_scores'][axis]) for r in rows)
        md = st.mean(abs(r['mono']['sub_scores'][axis] - r['ideal']['sub_scores'][axis]) for r in rows)
        winner = 'split' if sd < md else ('mono' if md < sd else 'tie')
        print(f'  {axis:<22}{sd:>10.4f}{md:>10.4f}{winner:>12}')

    # ---------------- (C) Verdict-bucket agreement with ideal --------
    def vmatch(score, ideal_score):
        return verdict(score) == verdict(ideal_score)
    split_v = sum(1 for r in rows if vmatch(r['split']['score'], r['ideal']['score']))
    mono_v  = sum(1 for r in rows if vmatch(r['mono']['score'],  r['ideal']['score']))
    print('\n--- (C) Verdict bucket agreement with IDEAL ---')
    print(f'  split: {split_v}/{n} = {split_v/n:.1%}')
    print(f'  mono:  {mono_v}/{n}  = {mono_v/n:.1%}')

    # Confusion (verdict distributions)
    from collections import Counter
    cnt = lambda key: Counter(verdict(r[key]['score']) for r in rows)
    print(f'  split verdict counts: {dict(cnt("split"))}')
    print(f'  mono  verdict counts: {dict(cnt("mono"))}')
    print(f'  ideal verdict counts: {dict(Counter(verdict(r["ideal"]["score"]) for r in rows))}')

    # ---------------- (D) Missing-must-haves Jaccard with IDEAL ------
    # Ideal "missing" = must_have_skills from gold that are NOT in CV (using canonical).
    def ideal_missing(cv, vac):
        must = [s for s in (vac.get('must_have_skills') or []) if isinstance(s, str) and s.strip()]
        cv_skills = (cv.get('technical_skills') or []) + (cv.get('domain_skills') or [])
        cv_exp = ic.expand_all(cv_skills)
        return [m for m in must if not ic.matches(m, cv_exp)]

    split_miss_jac = []
    mono_miss_jac = []
    for r in rows:
        gold_missing = ideal_missing(cvs[r['cv_id']], r['gold_vac'])
        sm = (r['split'].get('evidence') or {}).get('missing_must_haves', [])
        mm = (r['mono'].get('evidence')  or {}).get('missing_must_haves', [])
        split_miss_jac.append(token_jaccard(gold_missing, sm))
        mono_miss_jac.append(token_jaccard(gold_missing, mm))
    print('\n--- (D) missing_must_haves Jaccard vs IDEAL-missing ---')
    print(f'  split  mean={st.mean(split_miss_jac):.4f}  median={st.median(split_miss_jac):.4f}')
    print(f'  mono   mean={st.mean(mono_miss_jac):.4f}  median={st.median(mono_miss_jac):.4f}')
    ws = sum(1 for s, m in zip(split_miss_jac, mono_miss_jac) if s > m)
    wm = sum(1 for s, m in zip(split_miss_jac, mono_miss_jac) if m > s)
    print(f'  per-pair wins: split={ws} ({ws/n:.1%})  mono={wm} ({wm/n:.1%})')

    # ---------------- (E) Reason-text structure -----------------------
    print('\n--- (E) Reason text characteristics ---')
    def lens(key, lang):
        return [len((r[key].get(f'reason_{lang}') or '').split()) for r in rows]
    for key in ('split', 'mono'):
        en = lens(key, 'en')
        uk = lens(key, 'uk')
        print(f'  {key} reason_en words: mean={st.mean(en):.1f}  median={st.median(en):.1f}  >30={sum(1 for x in en if x>30)}')
        print(f'  {key} reason_uk words: mean={st.mean(uk):.1f}  median={st.median(uk):.1f}  >30={sum(1 for x in uk if x>30)}')

    # Reason hallucination check: any gap NOT in the missing_must_haves list?
    # (Both pipelines emit gaps in the text; we check whether the gap tokens are recoverable.)
    # Simple heuristic: gap is "Gaps: X, Y" / "Брак: X, Y"
    import re
    def extract_gap_terms(text):
        if not text: return []
        for marker in ('Gaps:', 'Прогалини:', 'Брак:'):
            i = text.find(marker)
            if i >= 0:
                tail = text[i+len(marker):]
                tail = re.split(r'[.;\n]', tail)[0]
                return [t.strip(' .,;') for t in re.split(r'[,;]', tail) if t.strip()]
        return []

    split_hallucinated = 0
    mono_hallucinated = 0
    for r in rows:
        s_gaps = extract_gap_terms(r['split'].get('reason_en') or '')
        m_gaps = extract_gap_terms(r['mono'].get('reason_en')  or '')
        s_missing = set(x.lower() for x in (r['split'].get('evidence') or {}).get('missing_must_haves', []))
        m_missing = set(x.lower() for x in (r['mono'].get('evidence')  or {}).get('missing_must_haves', []))
        for g in s_gaps:
            gl = g.lower()
            if gl in ('none', 'немає', 'нема', ''): continue
            if not any(gl in m or m in gl for m in s_missing):
                split_hallucinated += 1
                break
        for g in m_gaps:
            gl = g.lower()
            if gl in ('none', 'немає', 'нема', ''): continue
            if not any(gl in m or m in gl for m in m_missing):
                mono_hallucinated += 1
                break
    print(f'  split reason_en gaps not in missing_must_haves: {split_hallucinated}/{n}')
    print(f'  mono  reason_en gaps not in missing_must_haves: {mono_hallucinated}/{n}')

    # ---------------- (F) Repeat the user's previous biased analysis to demonstrate ----
    print('\n--- (F) DEMONSTRATING THE PRIOR BIAS ---')
    def ideal_must(vac):
        return [s for s in (vac.get('must_have_skills') or []) if isinstance(s, str) and s.strip()]
    split_must_jac = []
    mono_must_jac_polluted = []
    mono_must_jac_clean = []
    for r in rows:
        gold_must = ideal_must(r['gold_vac'])
        # split: read the v3 normalized vacancy's must_have_skills (clean signal)
        norm = load_json_safe(f'{BASE}/results/vacancy_20260522_194353/normalized/{r["vid"]}.json')
        split_must = (norm or {}).get('must_have_skills') or []
        split_must_jac.append(token_jaccard(gold_must, split_must))
        # mono polluted: matched_skills (contains nice-to-haves too!) ∪ missing_must_haves
        mev = (r['mono'].get('evidence') or {})
        matched = mev.get('matched_skills', [])
        missing = mev.get('missing_must_haves', [])
        polluted = list({*[s.lower() for s in matched], *[s.lower() for s in missing]})
        mono_must_jac_polluted.append(token_jaccard(gold_must, polluted))
        # mono CLEAN: gold_must that mono *also* extracted = (gold ∩ (matched + missing))
        # The cleanest fair signal: what fraction of gold_must does mono reproduce somewhere?
        mono_seen = set()
        for s in matched + missing:
            for v in ic.expand_one(s): mono_seen.add(v)
        recovered = sum(1 for g in gold_must if any(v in mono_seen for v in ic.expand_one(g)))
        recall = recovered / max(len(gold_must), 1)
        mono_must_jac_clean.append(recall)
    print(f'  PRIOR (biased) split mean Jaccard: {st.mean(split_must_jac):.3f}')
    print(f'  PRIOR (biased) mono  mean Jaccard: {st.mean(mono_must_jac_polluted):.3f}')
    print(f'  Mono RECALL of gold_must (clean): {st.mean(mono_must_jac_clean):.3f}')
    # Also compute split recall for symmetry
    split_recall = []
    for r in rows:
        norm = load_json_safe(f'{BASE}/results/vacancy_20260522_194353/normalized/{r["vid"]}.json')
        split_must = (norm or {}).get('must_have_skills') or []
        gold_must = ideal_must(r['gold_vac'])
        if not gold_must:
            split_recall.append(1.0); continue
        se = ic.expand_all(split_must)
        rec = sum(1 for g in gold_must if any(v in se for v in ic.expand_one(g))) / len(gold_must)
        split_recall.append(rec)
    print(f'  Split RECALL of gold_must (clean): {st.mean(split_recall):.3f}')

    # Persist a per-pair CSV for the user
    import csv
    out_csv = '/tmp/analysis/per_pair.csv'
    with open(out_csv, 'w', newline='', encoding='utf-8') as f:
        w = csv.writer(f)
        w.writerow(['cv_id','vacancy_id','ideal_score','split_score','mono_score',
                    'split_abs_dev','mono_abs_dev',
                    'ideal_verdict','split_verdict','mono_verdict',
                    'split_miss_jac','mono_miss_jac'])
        for r, sd, md, sm, mm in zip(rows, split_dist, mono_dist, split_miss_jac, mono_miss_jac):
            w.writerow([r['cv_id'], r['vid'],
                        round(r['ideal']['score'], 4),
                        round(r['split']['score'], 4),
                        round(r['mono']['score'], 4),
                        round(sd, 4), round(md, 4),
                        verdict(r['ideal']['score']),
                        verdict(r['split']['score']),
                        verdict(r['mono']['score']),
                        round(sm, 4), round(mm, 4)])
    print(f'\nPer-pair CSV: {out_csv}')


if __name__ == '__main__':
    main()
