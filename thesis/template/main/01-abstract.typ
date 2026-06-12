#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
#heading(numbering:none)[#i18n("abstract-title", lang:option.lang)] <sec:abstract>

When I look for an IT job in Ukraine, I have to visit several job sites. Each one has its own listings, format, and search options. For every vacancy, I decide one at a time whether the role fits. The work of comparing each vacancy to my own background is repeated by every candidate, every time, and across a full search it adds up to tens of hours of reading, while I am tired and making worse choices.

In this thesis I present Vakansio, a live personalised job-matching service that helps the candidate side of the search. The system collects vacancies from seven sources, turns them and the candidate's CV into structured data, and scores every (CV, vacancy) pair. The score comes from a deterministic seven-axis sub-score calculator, refined by a Composite Large-Language-Model Judge, and the result is presented with bilingual English and Ukrainian explanations. I implemented the back-end on Clean Architecture and the SOLID principles in .NET 8, the front-end in React and TypeScript, and host the service on Amazon Web Services. It is reachable on the open internet.

I propose a five-layer evaluation methodology: CV normalisation, vacancy normalisation, score, reason text, and ranking. It follows the Holistic Evaluation of Language Models framework, with a method matched to each layer's data type. The reason-text layer reports a significant improvement of the v6 production prompt over the v4 baseline (overall $Delta = +0.30$, 95 per cent paired-bootstrap CI $[+0.19, +0.40]$, $N approx 391$ pairs), judged by Claude Opus 4.7. The ranking layer is reported as directionally non-significant on a gold set of 14 CVs ($Delta "NDCG"@10 = -0.047$, 95 per cent paired-bootstrap CI $[-0.168, +0.065]$), judged by Claude Sonnet 4.6; a follow-up sensitivity check on the proposed calibration clamp moves the headline to $+0.018$, but the verdict stays inconclusive at the standard significance level. The methodology holds whatever the empirical verdict turns out to be, and I state openly what would be needed to settle the open question.

#v(2em)
#if doc.at("keywords", default:none) != none {[

  _*#i18n("keywords", lang: option.lang)*_:

  #enumerating-items(
    items: doc.keywords,
    italic: true
  )
]}
