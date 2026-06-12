#import "/local-lib/template-thesis.typ": *
#import "/metadata.typ": *
#pagebreak()
#heading(numbering:none)[#i18n("ack-title", lang:option.lang)] <sec:ack>

I would like to express my gratitude to my supervisor, *Volodymyr Skochko*, whose methodological guidance shaped both the architectural choices in this project and the way I framed the evaluation chapter. Your feedback turned several rough drafts into the structure the thesis stands on today, and the methodology pivot reported in Chapter 5 would not have landed cleanly without it. Many thanks for your time and your patience throughout the supervision process.

I also owe a great deal to several lecturers at the Kyiv School of Economics whose teaching shaped what I was able to build.

*Artem Korotenko*, the Academic Director, first showed me what programming actually is. Long before I could think about Clean Architecture or SOLID, his courses gave me the intuition for what a well-structured program even looks like. The discipline that the Domain layer of Vakansio enforces, with no external dependencies and every collaborator behind an interface, is the same discipline I first encountered in his classroom.

*Ihor Pysmennyi* taught me how production systems actually run. His DevOps course was the first place I saw containers, reverse proxies, and managed cloud services treated as concrete engineering choices rather than background magic. The Caddy-on-EC2 deployment that hosts Vakansio today is a direct descendant of the patterns he walked us through.

*Dmytro Nomirovskiy* ran the toughest mathematics courses of my degree. The classes were not easy, but the habit of working through a problem until the proof actually closes is something I have carried into every layer of this project, including the statistical inference that backs Chapter 5.

Finally, *Vadym Yeremenko* delivered the clearest lectures I had on programming paradigms, networking, and C++. The instinct to reach for the simpler abstraction when a problem feels tangled, and to treat the wire format as a first-class contract, came from his classes and shows up everywhere from the layer interfaces in Domain to the v6.7.8 contract drift fix described in Chapter 4.
