#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
= #i18n("analysis-title", lang:option.lang) <sec:analysis>

== The Ukrainian job-search landscape <sec:analysis:landscape>

Before I started designing Vakansio, I looked carefully at the Ukrainian IT market and at the way candidates search for jobs in it. The market itself is large enough to make manual job search time-consuming. At any given moment, tens of thousands of open positions are visible across the various Ukrainian job sites, with new vacancies appearing daily. Candidates looking for a position typically check several sites in parallel, because no single source covers the whole market.

The landscape is fragmented. Different sites serve different audiences. Some focus specifically on IT and product roles, some are general-purpose and cover every industry, some are international networks with a Ukrainian segment, and at least one is a meta-aggregator that re-publishes listings from other boards. Each site stores its own catalogue, requires its own account, and presents listings in its own format. A candidate cannot search across all sources from a single place — they have to repeat the same query, with the same intent, on each site separately.

Within each site, the ranking of returned results is rarely personalised to an externally uploaded CV. The default ordering is usually by date of posting, and the available filters are coarse — location, seniority, salary range, sometimes a fixed set of technology tags. Where a site does offer a personalised signal at all, it operates on the candidate's structured on-platform profile rather than on a free-form CV document, and the score is provided without an explanation of how it is derived.

These two problems — split sources and no CV-based ranking with explanation — define the gap I built Vakansio to fill. A service that aggregates vacancies from multiple Ukrainian sources and ranks them against an uploaded CV, with a human-readable explanation, is not offered by any mainstream Ukrainian platform today.

== Existing job sites and what they offer <sec:analysis:sites>

To make the gap concrete, I profiled the major sites that a Ukrainian IT candidate is likely to use. For each site I list what it does well and where it falls short for the specific problem of ranking an externally uploaded CV against multi-source listings with an explanation.

*Djinni.co* is an IT-focused board with a candidate-oriented design. It is the largest Ukrainian tech job marketplace, with around fifty thousand developers using the site each month. Candidates create a profile with a declared salary expectation, a technology stack, and a seniority level, and employers see anonymised candidate cards until the candidate chooses to reveal contact details.

Strengths:
- Transparent salary practices — every candidate lists net monthly salary expectations
- Strong anonymity model that protects candidates from their current employer
- Precise filters within the IT domain (language stack, salary range, English level, country, employment type)
- Free for candidates, with no hidden fees

Weaknesses:
- Default ordering of the result list is reverse-chronological
- No ranking against an externally uploaded CV document
- Coverage is limited to IT and adjacent roles
- The match signal that exists relies on the platform's own structured profile, not on the candidate's CV file

*DOU.ua* is one of the oldest Ukrainian developer communities, founded in 2005, with an integrated job board at #raw("jobs.dou.ua"). Listings tend to be detailed and written by employers themselves, and the board provides a public RSS feed for automated consumption. The search interface offers keyword and category filters.

Strengths:
- Detailed listings written directly by employers
- Public RSS feed that supports automated aggregation
- Strong community presence with company reviews and salary surveys

Weaknesses:
- No personalised ranking — listings are sorted by date
- Search is limited to keyword and category filters
- Volume is lower than the general-purpose boards
- Strongest as a passive notice-board for the community rather than as a high-volume search tool

*Robota.ua* is a general-purpose Ukrainian job board covering every industry, with around 400,000 active job seekers and membership in the Pracuj.pl network. In 2024–2025 the platform launched an AI Job Search Agent that creates resumes, suggests personalised vacancies, and guides the candidate through a messaging-style interface.

Strengths:
- Wide coverage across industries — retail, manufacturing, white-collar, IT
- Standard filters for industry, region, schedule, and salary
- New AI Job Search Agent provides personalised suggestions and resume help

Weaknesses:
- The AI Agent operates on the platform's own structured profile, not on an externally uploaded CV document
- Suggestions are provided without a human-readable explanation of why a particular vacancy was picked
- Aggregation from external sources is not part of the offering

*Work.ua* is the largest Ukrainian job board by traffic, with several million unique monthly visitors and around three and a half million candidate accounts. It covers every industry and hosts the largest Ukrainian CV database by volume.

Strengths:
- Largest reach in Ukraine by visitor count
- Very large active CV database
- Broad cross-industry coverage and standard search filters
- Strong recruiter-side tools and trust algorithms introduced in 2025

Weaknesses:
- No personalised ranking based on an externally uploaded CV
- Relevance signal is keyword presence and selected filters
- No explanation accompanies the ordering of returned results
- No aggregation from competing sources

*LinkedIn* is an international professional network with a Ukrainian segment. Its jobs ranking uses more than three hundred signals built on top of the candidate's profile, connections, and engagement on the platform.

Strengths:
- Sophisticated ranking based on a Semantic Skill Graph
- Personalisation tied to the candidate's full profile and network
- International coverage including Ukrainian employers

Weaknesses:
- Ranking is driven by the LinkedIn profile, not by an uploaded CV document — candidates with a thin LinkedIn profile but a rich CV do not benefit
- Strict restrictions on automated access (see @sec:analysis:legal)
- No transparent explanation of why a particular vacancy was ranked highly

*Jooble* is a meta-aggregator founded in Ukraine in 2006. It collects listings from more than fifteen thousand public job sources worldwide and presents them in a single interface with a link out to the original posting; it is the second largest job search engine in the world by reach.

Strengths:
- Very broad coverage across many national job boards in a single interface
- Strong international reach and a Ukrainian core team
- Direct keyword search across many sources at once

Weaknesses:
- No ranking against an uploaded CV — the user sees a long list of links sorted by the same default signals as the originating sources
- Results lead out to other platforms, so the experience is not continuous
- No explanation accompanies the ordering

Several smaller and niche boards (Grc.ua, Happy Monday, Lobby.ua, Jobs.ua) operate alongside the six profiled above; they follow the same general pattern and I do not profile them separately here.

Every site I surveyed follows the same pattern: keyword search, date ordering, and coarse filters. Where a personalised signal exists at all, it operates on the site's own structured profile rather than on an externally uploaded CV, and it is provided without an accompanying explanation. None of the surveyed sites ranks an externally uploaded CV against multi-source listings with a human-readable explanation of why a particular listing received its position. This is the specific gap that I designed Vakansio to fill, and it is restated as a concrete requirement in @sec:analysis:requirements.

== Legal and technical constraints on data collection <sec:analysis:legal>

Pulling from multiple job sources raises two questions before any commercial launch: is the data collection legally defensible, and is the channel sustainable in production? I first surveyed the legal landscape in Ukraine and the European Union, then summarised the per-source position with respect to commercial use, and finally outlined the alternative service providers that a market version of Vakansio would require.

*Legal landscape.* The biggest risk for a Ukrainian company aggregating job listings is not criminal liability under Article 361 of the Criminal Code (unauthorised interference with information systems). Nor is it GDPR applied to vacancy text — that text, without personal identifiers, is not personal data. The main risk is civil. The 2022 Law of Ukraine on Copyright and Related Rights (No. 2811-IX), in force since 1 January 2023, introduced a sui generis database right that protects, for fifteen years, databases the maker has invested substantially in. Sites such as Work.ua and Robota.ua are likely to qualify, although no Ukrainian court has yet ruled on this in the context of job-board aggregation. The controlling European Union precedent for this kind of situation is the Court of Justice ruling in CV-Online Latvia v. Melons #cite(<cjeuCvOnlineMelons2021>), in which the operator of a Latvian meta-search engine indexing job listings was found to "extract and re-utilise" the database of CV-Online; the Court held that such acts may be prohibited by the database maker only where they adversely affect the investment in obtaining, verifying, or presenting the database contents. The ruling is directly applicable to the situation of Vakansio.

A separate consideration applies where individual listings contain incidental personal data — for example, named recruiter contacts or sole-proprietor employer details. Any such data I ingest through the prototype's scraping channels is processed under the same legitimate-interest basis or stripped on ingestion. The collection of CV files uploaded by the candidate is governed by a different basis. CV files contain personal data and trigger the full set of obligations under the Ukrainian Law on the Protection of Personal Data and, where the candidate is in the European Union, the General Data Protection Regulation. These obligations include an explicit lawful basis for the processing, retention limits, and the candidate's right to access and deletion. I honour them in Vakansio through explicit consent at upload time, scope limitation (CVs are used solely for matching), and deletion on account closure.

Recent litigation against scrapers of large platforms provides relevant context. In December 2022 the hiQ Labs v. LinkedIn proceedings ended with a stipulated judgment under which hiQ paid USD 500,000 and accepted a permanent injunction. This outcome stood despite an earlier appellate ruling that scraping publicly accessible data is not a violation of the United States Computer Fraud and Abuse Act. In July 2025 the LinkedIn data provider Proxycurl shut down following a permanent injunction in LinkedIn v. Proxycurl. These outcomes indicate that, while scraping public data is generally not a criminal matter, civil liability arising from breach of platform terms of service is real and has materialised against well-funded specialised providers.

*Per-source status.* The seven sources I currently use in the Vakansio prototype have different statuses with respect to commercial use. @tab:analysis:sources summarises each one, together with the channel that a commercial version would have to adopt.

#figure(
  table(
    columns: 3,
    stroke: none,
    align: (left, left, left),
    inset: 6pt,
    table.header([*Source*], [*Status for commercial use*], [*Channel for product*]),
    [DOU.ua], [Permitted via official RSS feed], [Keep current RSS channel],
    [Jooble], [Permitted via official REST API], [Keep current API channel],
    [Djinni.co], [Negotiable through partnership], [Partner API via direct outreach],
    [Robota.ua], [Restricted; aggregator access disallowed], [Business-development partnership],
    [Work.ua], [Restricted by terms of service], [Partnership through partner contacts],
    [LinkedIn], [Prohibited; high legal risk], [Paid licensed data provider],
    [Manual URL], [User-driven, no policy issue], [Keep as is],
  ),
  caption: [Per-source status for commercial use and the channel a market version would adopt.],
) <tab:analysis:sources>

*Paid replacement for LinkedIn.* Direct scraping of LinkedIn is not a sustainable commercial channel; replacement is therefore required. Several paid providers offer LinkedIn job-listing data on more clearly licensed terms. Apify, Bright Data, and JSearch make the data available through scraping-as-a-service offerings at approximately USD 100–300 per month for the volume needed by a small commercial deployment; these providers outsource the technical collection but do not assume contractual liability for breaches of LinkedIn terms of service. Coresignal positions itself as a licensed data vendor and provides equivalent coverage at approximately USD 1,000 per month under a negotiated commercial agreement, which is the option offering the most clearly delineated legal basis for a commercial launch. The LinkedIn Talent Solutions partner programme is, in practice, not accessible to a pre-revenue startup.

*Path to commercial deployment.* The mix I propose for a market version of Vakansio is therefore as follows. The free legitimate channels — the DOU RSS feed and the Jooble API — remain unchanged. A new channel is added through the OLX Partner API, which the prototype does not currently use. A parallel applicant-tracking-system aggregation channel is built on top of the free public boards exposed by Greenhouse, Lever, Workable, and SmartRecruiters; this channel covers Ukrainian-relevant employers such as Grammarly, GitLab, MacPaw, Kyivstar, and Ajax Systems, and brings several thousand additional openings without per-listing cost. Direct scraping of Djinni, Robota.ua, and Work.ua is replaced by partnership-based access through the data-licensing routes described above. Direct scraping of LinkedIn is replaced by a licensed paid provider — Coresignal for a production launch on a clearly delineated legal basis, or Apify or Bright Data for a minimum-viable product.

The Vakansio implementation I evaluate in this thesis is a capstone prototype. The scraping channels currently in use are appropriate for that purpose: they access only publicly visible listings, do not bypass authentication, respect robots.txt where it expresses an explicit position with respect to the public listing pages, and operate at human-level rates. Commercial deployment would replace those channels with the official, partnership, and paid options described above.

== Related work in recommender systems <sec:analysis:recsys>

A recommender system is software that suggests items to a user — products in an online shop, films on a streaming service, or vacancies on a job board. Recommender systems have been studied since the mid-1990s, and Gediminas Adomavicius and Alexander Tuzhilin #cite(<adomaviciusTowardNextGeneration2005>) distinguish three main families of approach.

The first family is *content-based*. The system looks at facts the user has shared about themselves — for Vakansio, the uploaded CV — and finds items whose own description matches those facts. If the CV says the candidate knows React and TypeScript and has three years of experience, a content-based recommender will rank highly the vacancies that ask for React, TypeScript, and three or more years of experience.

The second family is *collaborative filtering*. The system looks at what other users have done — which items they liked, viewed, or applied to — and recommends the items that similar users picked. For Vakansio, this would mean ranking a vacancy highly because other candidates with a profile similar to the current candidate's applied to it.

The third family is *hybrid*. The system combines content-based matching and collaborative filtering and weighs the two signals together.

I chose the content-based family for Vakansio. There are three reasons. First, a candidate who signs up for the first time has no past behaviour for the system to learn from, which means a collaborative filter would have nothing to compare against. Second, the CV and the vacancy both contain structured fields. This makes attribute matching simple. Third, the candidate expects an explanation of why a vacancy was ranked highly, and content-based matching can give that explanation directly, because the match is described using attributes from the candidate's CV.

A separate question is how to measure whether a ranked list is good. A standard answer in information retrieval is a metric called *normalised discounted cumulative gain* (NDCG). The idea is simple: items shown higher in the list have more impact on the user, so the metric gives more weight to relevance at the top of the list and less to relevance at the bottom. The variant NDCG\@10 measures the quality of the first ten results. It considers both how relevant each result is and how high it appears in the list. NDCG was first defined by Kalervo Järvelin and Jaana Kekäläinen #cite(<jarvelinCumulatedGainBased2002>). In this thesis I compute NDCG\@10 in a separate offline analysis presented in @sec:validation; it is not part of the production scoring pipeline.

== LLM-based evaluation frameworks <sec:analysis:llm-eval>

Evaluating the output of a large language model is harder than evaluating a classifier. The output is free-form text rather than a single label, and several different answers can all be reasonable. Two recent lines of work address this difficulty and are relevant to my evaluation methodology.

The first is HELM — Holistic Evaluation of Language Models — published by Percy Liang et al. #cite(<liangHolisticEvaluation2023>) at Stanford. The main idea of HELM is to evaluate a language model on many separate metrics rather than a single score. Instead of asking whether the model is good in general, HELM asks how accurate it is on a given task, how robust it is to small input changes, how calibrated its confidence scores are, and how fair it is across different groups of inputs, among other dimensions. Each metric is computed separately, so that strengths and weaknesses become visible. I borrow this multi-metric idea: every stage of the pipeline — CV normalisation, vacancy normalisation, sub-score correctness, ranking quality, and reason quality — is evaluated on its own metric, instead of a single accuracy number that hides where the system fails. The full layered methodology is described in @sec:validation.

The second is LLM-as-Judge — using a strong language model as the evaluator for the output of another model — proposed by Lianmin Zheng et al. #cite(<zhengJudgingLLMasaJudge2023>) in the context of MT-Bench and Chatbot Arena. The argument is empirical: when human annotators and a strong language-model judge (GPT-4 in the original paper) are asked to compare the same pair of answers, they agree more than 80% of the time. This makes the language-model judge a cheaper substitute for human ratings, especially when the evaluation set is too large to be rated by hand. I use the LLM-as-Judge pattern in two places in Vakansio. Inside the scoring pipeline, a Composite Judge step refines the deterministic scores against a per-domain rubric. Inside the evaluation, a separate large language model is asked to rate a held-out set of CV–vacancy pairs on an ordinal scale; those ratings serve as the ground truth against which I compare the production ranking in @sec:validation.

== Gaps and my positioning <sec:analysis:gaps>

Sections @sec:analysis:landscape to @sec:analysis:llm-eval surveyed two bodies of work that are relevant to Vakansio: the existing Ukrainian job-search market on the product side, and the academic literature on recommender systems and language-model evaluation on the methodology side. I now pull the two together and state explicitly what Vakansio is and how it relates to each.

The product-side gap is specific. None of the Ukrainian job sites offers a personalised ranking of multi-source vacancies against an externally uploaded CV, accompanied by a human-readable explanation of why each match was made. This is true for the IT-focused boards such as Djinni and DOU, for the general-purpose boards such as Robota.ua and Work.ua, and for the international platforms such as LinkedIn and Jooble. Where partial elements exist — for example Robota.ua's AI Job Search Agent over its on-platform profile, or LinkedIn's ranking based on the LinkedIn profile and engagement signals — they are tied to a single source's internal representation of the candidate, and they are not accompanied by an explanation. I built Vakansio to fill this combined gap.

On the methodology side, I apply a specific combination of ideas from recent work. Vakansio is a content-based recommender (@sec:analysis:recsys), which is the natural choice for a system that has to deliver useful recommendations from the candidate's very first session and that has to explain each recommendation in terms of the candidate's own CV. The scoring pipeline computes seven deterministic sub-scores and refines them through a Composite Judge step that follows the LLM-as-Judge pattern (@sec:analysis:llm-eval). The evaluation methodology described in @sec:validation follows HELM's multi-metric approach (@sec:analysis:llm-eval) — each stage of the pipeline has its own metric — and uses an LLM-as-Judge to construct the held-out ratings against which I measure ranking quality.

My contribution is not in any single one of these elements. Content-based recommendation is well established, the LLM-as-Judge pattern has been published, and multi-metric evaluation is the standard direction of the field. My contribution is the combination: a multi-source CV-driven matching service for the Ukrainian market, delivered with explanations, built on a hybrid scoring pipeline that combines deterministic sub-scores with an LLM judge, and evaluated with a layered methodology that reports each stage's quality separately instead of rolling everything up into one number.

== Functional and non-functional requirements <sec:analysis:requirements>

The work in Sections @sec:analysis:landscape to @sec:analysis:gaps motivates a specific set of requirements that Vakansio must satisfy. I group the requirements into two families. The first family captures what the system must do, expressed as user-testable behaviour. The second family captures how well the system must do it, as properties that matter once the system is deployed.

*Functional requirements.* The system must:

+ *FR1 — Account management.* Allow a candidate to register an account and log in.
+ *FR2 — CV upload and parsing.* Accept a CV as a PDF upload and parse it into a structured representation.
+ *FR3 — Multi-source search.* Search for vacancies from several external sources in response to a keyword query.
+ *FR4 — Personalised ranking.* Return a ranked list of vacancies, ordered by the system's estimate of fit between the uploaded CV and each vacancy.
+ *FR5 — Verdict and explanation.* Show, for each returned vacancy, a numerical match score between zero and one, a verdict label (Strong, Partial, Weak, or Mismatch), the matched skills, the missing must-have skills, and a short bilingual explanation in English and Ukrainian.
+ *FR6 — Filtering.* Support filtering of returned results by source, seniority level, and work format.
+ *FR7 — Application tracker.* Allow a candidate to save vacancies of interest to a personal application tracker and to update their application status.

*Non-functional requirements.* The system must satisfy the following properties:

+ *NFR1 — Public deployment.* The system is reachable on the open Internet at a stable URL, s