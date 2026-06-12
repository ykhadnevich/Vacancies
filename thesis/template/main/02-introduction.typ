#import "@preview/hei-synd-thesis:0.1.1": *
#import "/metadata.typ": *
#pagebreak()
= #i18n("introduction-title", lang:option.lang) <sec:intro>

== Initial situation <sec:intro:situation>

When I started looking for an IT job in Ukraine, I quickly noticed how fragmented the market is. To cover it properly, I had to visit many different websites. Each one keeps its own list of vacancies, in its own format, with its own search and filter options. There is no single place where I could see everything at once, so I went through site by site, opening vacancy after vacancy.

For every vacancy I had to read the description, the list of requirements, and the company information, and then decide whether the job actually fits me. I answered the same questions each time: does the role match my background, do I have the listed skills, is the seniority right, is my English good enough, is the salary acceptable. Reading and judging a single vacancy takes a few minutes. A typical search returns dozens, sometimes hundreds of postings, so I spent many evenings on this before I even had a shortlist.

None of the popular Ukrainian IT job sites do this work for me. They show vacancies by date or in the order the site chooses, but none of them tells me, before I start reading, that one vacancy is a strong fit and another is not. I realised that this work — comparing each vacancy with my own CV — is repeated by every candidate, every time they look for a new job.

== Motivation <sec:intro:motivation>

For most people, looking for a new job is one of the more stressful and time-consuming tasks they face during their career, and I experienced this directly. It is rarely just an evening of clicking through listings. When I wanted to find a position that truly fits, I had to spend weeks on it: opening dozens of vacancies a day, comparing each one against my own background, preparing tailored applications, and keeping track of where I had already applied.

The time cost was the most visible part. When I spent three to five minutes carefully reading one vacancy and another minute or two deciding whether to apply, a single evening covered roughly twenty or thirty postings. I had to do this on top of studies and other work, so across a full search I accumulated thirty to fifty hours just on the evaluation phase, before counting interviews, technical tasks, and follow-up emails.

The hidden cost was the cognitive load. Every vacancy made me switch context — different company, different stack, different seniority, different country, sometimes a different language. Doing this for hours, day after day, leads to decision fatigue #cite(<danzigerExtraneousFactors2011>). By the end of an evening, the vacancies started to blur together, and I noticed that I began to make worse decisions or skip listings I should have read more carefully. I missed some good matches; I pursued some bad ones.

The emotional cost was also significant. A long search without visible progress reduces motivation, and repeated rejection — or, more often, no reply at all — made me doubt my own qualifications. The longer the search took, the more it affected my focus and effort, which in turn slowed the search further.

I realised that a tool that takes on part of this work — that reads each vacancy carefully, compares it against my CV, and tells me in advance where to focus — would have clear value. Saving even half of the evaluation time, and surfacing the most promising vacancies first, would turn the job search from a lengthy and exhausting process into one that is much more manageable. This is the problem I set out to solve with Vakansio.

== Objectives <sec:intro:objectives>

I built Vakansio around four explicit objectives. Each one is restated as a measurable goal in a later chapter:

+ *A working public service.* The system must be reachable on the open internet, support user registration and login, accept a CV as a PDF upload, search for vacancies from several sources, and return a personalised list with verdicts and explanations. The deployed instance is available at #link("https://dsus1dizgh006.cloudfront.net")[dsus1dizgh006.cloudfront.net] and supports real user accounts.

+ *A disciplined software architecture.* The code base has to follow the Clean Architecture pattern proposed by Robert C. Martin #cite(<martinCleanArchitecture2017>) and the five SOLID principles. This is not a stylistic choice: the layer separation has to be visible in the code — the Domain project must not reference the Infrastructure project, and abstractions must live in the layer that needs them. The SOLID principles must be traceable to concrete artefacts. @sec:design documents the mapping in detail.

+ *Measurable match quality.* An informal impression is not enough. I defined an evaluation methodology that another engineer could repeat. The primary level compares the deterministic field-by-field outputs of the pipeline against my own curated reference normalisations of CVs and vacancies, using mean absolute error and verdict agreement, computed by the evaluation tool that ships with the code base. The secondary level uses an LLM as judge to rate match quality on a held-out batch of CV–vacancy pairs; ranking-quality numbers derived from these ratings are reported alongside the primary results in @sec:validation.

+ *Cost transparency for every LLM call.* Each call must be logged with the pipeline stage that issued it, the number of input and output tokens, and the computed dollar cost. The data is written to a PostgreSQL table and is queryable by an operator. Per-stage cost numbers and aggregate statistics appear in @sec:validation.

Several topics are out of scope. The service does not allow employers to post their own vacancies — vacancies are aggregated from external sources only. There is no employer-side feature for searching candidates. The deployment is single-tenant: there is no organisation account model. I did not run an external user study with multiple real candidates; the evaluation is internal, against the gold sets described in @sec:validation, and the implications of this choice are discussed there.

== Method <sec:intro:method>

I built the system as a four-layer .NET 8 back-end, a React and TypeScript front-end, and a small AWS deployment. The four layers are Domain, Application, Infrastructure, and API. The Domain layer contains entities, value objects, and pure scoring rules that do not depend on any external library. The Application layer runs the use cases — searching, scoring, and returning results — following the Command-Query Responsibility Segregation pattern. The Infrastructure layer adapts external dependencies: the Gemini large language model, the PostgreSQL database, Amazon S3 for CV files, and seven scrapers, APIs, and RSS feeds that fetch vacancies from job sites. The API layer is a thin HTTP entry point: controllers, authentication, and routing.

My scoring approach is hybrid. Some parts of candidate–vacancy fit can be computed precisely: whether the listed skills overlap, whether the seniority level matches, and how many years of experience the candidate has, among other measurable signals. These parts are computed by deterministic C\# code organised as seven sub-scores: skill, seniority, experience, role intent, domain, language, and education. Their weighted sum is the first numeric estimate of fit. Other parts of the judgement are harder to express as fixed rules — for example, whether the vacancy description asks for the same kind of work the candidate has actually done. For these parts, the system calls Gemini 2.5 Flash to act as a Composite Judge that refines the deterministic score against a per-domain rubric. A second batched LLM call generates short bilingual explanations in English and Ukrainian for the top results.

The infrastructure runs on Amazon Web Services. A single t3.micro EC2 instance hosts the API container behind Caddy. The database is an Amazon RDS PostgreSQL instance. CV files live in an Amazon S3 bucket. The front-end bundle is served from a second S3 bucket through CloudFront.

I worked iteratively, around versioned releases of the scoring pipeline instead of fixed-length sprints. The methodology, coding standards, and quality-assurance approach are documented in @sec:impl.

This summary covers the high-level shape only; the architecture, the scoring pipeline, the technology choices, and the deployment story are documented in the chapters that follow.

== Structure of this thesis <sec:intro:structure>

I organise the remainder of the document as follows.

#emph[@sec:analysis] surveys the Ukrainian job-search landscape, the approaches of the main job sites, and related work in recommender systems and large-language-model evaluation. It also lists the functional and non-functional requirements that Vakansio has to satisfy.

#emph[@sec:design] presents the architectural drivers, the technology stack with justification against alternatives, the four-layer Clean Architecture decomposition, the scoring pipeline and Composite Judge, the cache hierarchy, the user-experience approach, and the mapping of the SOLID principles onto concrete artefacts in the code base.

#emph[@sec:impl] walks layer by layer through the Domain, Application, Infrastructure, and front-end implementations, documents the deployment and operations story on AWS, and describes two engineering challenges encountered during development.

#emph[@sec:validation] details the evaluation methodology, the construction of the gold sets, the primary numerical results, the secondary LLM-as-judge analysis, and the threats to validity, with an end-to-end worked example in the appendix.

#emph[@sec:conclusion] revisits the four objectives stated in this introduction, recounts the difficulties encountered, and outlines future work.
