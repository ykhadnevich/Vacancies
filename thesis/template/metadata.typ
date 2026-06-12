//-------------------------------------
// Document options
//
#let option = (
  type : "final",
  //type : "draft",
  lang : "en",
  template    : "thesis"
)
//-------------------------------------
// Optional generate titlepage image
//
#import "@preview/fractusist:0.1.1":*
#let project-logo= dragon-curve(
  12,
  step-size: 10,
  stroke-style: stroke(
    paint: gradient.radial(..color.map.rocket),
    thickness: 3pt, join: "round"
  ),
  height: 5cm,
  fit: "contain",
)

//-------------------------------------
// Metadata of the document
//
#let doc= (
  title    : "Vakansio: A Personalized Job-Matching Service",
  subtitle : "Clean Architecture, LLM-Based Scoring, and Gold-Set Evaluation on .NET 8",
  author: (
    name        : "Yan Khadnevich",
    email       : "ykhadnevich@kse.org.ua",
    degree      : "Bachelor",
    affiliation : "KSE",
    place       : "Kyiv",
    url         : "https://dsus1dizgh006.cloudfront.net",
    signature   : none,
  ),
  keywords : (
    "Job Matching",
    "Large Language Models",
    "Clean Architecture",
    ".NET 8",
    "Recommender Systems",
    "LLM-as-Judge",
  ),
  version  : "v1.0.0",
)

#let summary-page = (
  logo: project-logo,
  //one sentence with max. 240 characters, with spaces.
  objective: [
    I present Vakansio, a personalised job-matching service that aggregates vacancies from multiple Ukrainian sources and ranks them against an uploaded CV using a hybrid deterministic and LLM-based scoring pipeline.
  ],
  //summary max. 1200 characters, with spaces.
  content: [
    In this bachelor thesis I present Vakansio, a production-deployed job-matching service that helps candidates discover relevant vacancies across seven Ukrainian-market sources (Djinni, Robota.ua, DOU, LinkedIn, Jooble, Work.ua, and a manual-URL fallback). The system parses an uploaded CV and ranks each vacancy against it using a hybrid scoring pipeline that combines seven deterministic sub-score axes (skill, role intent, seniority, experience, domain alignment, language, and education) with a Gemini-based Composite Judge that anchors the final ranking to a per-family rubric.

    I implemented the service on .NET 8 with a Clean Architecture layering and a React/TypeScript frontend, hosted on AWS (EC2, RDS PostgreSQL, S3, and CloudFront). A four-tier cache hierarchy and a per-stage cost ledger keep the system economically viable at the production scale of a single-author project.

    My evaluation follows a five-layer methodology inspired by the Holistic Evaluation of Language Models framework. Layer 4 (reason quality) reports a statistically significant improvement of the v6 prompt over the v4 baseline (overall $Delta = +0.30$, 95% paired-bootstrap CI $[+0.19, +0.40]$), judged by Claude Opus 4.7. Layer 5 (ranking) reports an inconclusive primary endpoint ($Delta$ NDCG@10 $= -0.047$, CI $[-0.168, +0.065]$), judged by Claude Sonnet 4.6; a post-hoc sensitivity check shifts the headline to $+0.018$ without changing the verdict. Graceful-degradation patterns are discussed throughout the chapters.
  ],
  address: [Kyiv School of Economics #sym.bullet 3 Mykoly Shpaka Street #sym.bullet Kyiv 03113, Ukraine \ #link("mailto:info@kse.org.ua")[info\@kse.org.ua] #sym.bullet #link("https://kse.org.ua")[kse.org.ua]]
)

#let professor= (
  affiliation: "KSE",
  name: "Volodymyr Skochko",
  email: "vskochko@kse.org.ua",
)
#let expert= (
  affiliation: "KSE",
  name: "TBD",
  email: "expert@kse.org.ua",
)
#let school= (
  name: none,
  orientation: none,
  specialisation: none,
)
#if option.lang == "de" {
  school.name = "Hochschule für Ingenieurwissenschaften Wallis, HES-SO"
  school.orientation = "Systemtechnik"
  school.specialisation = "Infotronics"
} else if option.lang == "fr" {
  school.name = "Haute École d'Ingénierie du Valais, HES-SO"
  school.shortname = "HEI-Vs"
  school.orientation = "Systèmes industriels"
  school.specialisation = "Infotronics"
} else {
  school.name = "Kyiv School of Economics"
  school.shortname = "KSE"
  school.orientation = "Software Engineering & Business Analysis"
}

#let date = (
  submission: datetime(year: 2026, month: 6, day: 3),
  mid-term-submission: datetime(year: 2026, month: 4, day: 30),
  today: datetime.today(),
)

#let logos = (
  main: project-logo,
  topleft: (if option.lang == "fr" or option.lang == "de" {
    image("/resources/img/logos/hei-defr.svg", width: 6cm)
  } else {
    image("/resources/img/logos/kse_logo_horizontal_primary.png", width: 6cm)
  }),
  topright: image("/resources/img/logos/kse_logo_horizontal_primary.png", width: 0pt),
)


//-------------------------------------
// Settings
//
#let tableof = (
  toc: true,
  tof: false,
  tot: false,
  tol: false,
  toe: false,
  maxdepth: 3,
)

#let gloss    = true
#let appendix = true
#let bib = (
  display : true,
  path  : "/tail/bibliography.bib",
  style : "ieee", //"apa", "chicago-author-date", "chicago-notes", "mla"
)
go-author-date", "chicago-notes", "mla"
)
