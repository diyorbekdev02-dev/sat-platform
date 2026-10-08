using SatPlatform.Data;

namespace SatPlatform.Services;

public static partial class DemoContent
{
    // ================================================================= Reading & Writing (original short items, template based)

    private static IEnumerable<DemoQuestion> ReadingWriting(Random r)
    {
        foreach (var q in WordsInContext(r)) yield return q;
        foreach (var q in Boundaries(r)) yield return q;
        foreach (var q in FormStructureSense(r)) yield return q;
        foreach (var q in Transitions(r)) yield return q;
        foreach (var q in ShortPassages(r)) yield return q;
    }

    private static Difficulty D(int i, int n) => i < n / 3 ? Difficulty.Easy : i < 2 * n / 3 ? Difficulty.Medium : Difficulty.Hard;

    // ---- Words in Context: a sentence with a blank, the correct word and three distractors ----
    private static IEnumerable<DemoQuestion> WordsInContext(Random r)
    {
        (string Sentence, string Correct, string[] Wrong, string Why)[] bank =
        [
            ("The committee's decision to delay the vote was ______: with several members absent, any result would have been questioned.", "prudent", ["hasty", "trivial", "reluctant"], "The reasoning shows a careful, wise decision."),
            ("Although the author's early novels were praised for their ______ plots, her later books became hard to follow.", "straightforward", ["elaborate", "ambiguous", "unpredictable"], "“Although” contrasts with “hard to follow,” so the early plots were simple and clear."),
            ("The biologist has long been an ______ of ocean conservation, urging governments to protect marine habitats.", "advocate", ["opponent", "observer", "imitator"], "Someone who urges protection is a supporter, an advocate."),
            ("The building's design was deliberately ______: every surface was stripped of ornament so that proportion alone carried its beauty.", "austere", ["ostentatious", "intricate", "haphazard"], "Stripped of ornament means severely simple, austere."),
            ("Because the vaccine must be kept extremely cold, delivering it to remote villages has proved ______.", "challenging", ["effortless", "optional", "customary"], "Strict storage makes delivery difficult."),
            ("The physicist describes the ______ moment when, after years of failure, the detector finally registered the predicted signal.", "pivotal", ["mundane", "tedious", "incidental"], "A long-awaited breakthrough is a crucial, pivotal moment."),
            ("Economists caution that the rise in prices may be ______ rather than lasting, since it coincides with a temporary shortage.", "transient", ["permanent", "deliberate", "widespread"], "“Rather than lasting” and “temporary” point to short-lived."),
            ("The exhibit is remarkably ______, bringing together coins, textiles, maps, and manuscripts from a dozen countries.", "comprehensive", ["narrow", "outdated", "brief"], "Many kinds of objects from many places make it comprehensive."),
            ("The speech was so ______ that even experienced reporters could not say which position the senator supported.", "equivocal", ["emphatic", "coherent", "concise"], "Listeners could not tell her position, so the speech was deliberately ambiguous."),
            ("Engineers chose a ______ material for the bridge cables, one that would bend under heavy wind without snapping.", "resilient", ["brittle", "ornamental", "porous"], "A material that bends without breaking is resilient."),
            ("The historian's account is admirably ______; every claim is supported by a dated document.", "rigorous", ["careless", "speculative", "fanciful"], "Supporting every claim with evidence is rigorous."),
            ("Once common across the plains, the species is now so ______ that researchers see only a handful each year.", "scarce", ["abundant", "aggressive", "familiar"], "Only a handful seen each year means the species is scarce."),
            ("Rather than ______ the criticism, the director invited the critics to a second screening and answered their questions.", "dismissing", ["welcoming", "repeating", "anticipating"], "“Rather than” contrasts with inviting critics in, so she did not dismiss the criticism."),
            ("The two accounts of the battle are ______: one says the attack began at dawn, the other at dusk.", "contradictory", ["consistent", "detailed", "anonymous"], "Dawn versus dusk is a contradiction."),
            ("The village's water supply is ______ on a single well, so a dry season can leave residents without water for weeks.", "dependent", ["indifferent", "abundant", "secure"], "A single source on which everything relies means the supply is dependent on it."),
            ("Despite its ______ size, the pocket-sized device can translate speech in forty languages.", "modest", ["enormous", "impressive", "irregular"], "“Despite” contrasts a small size with a large capability."),
            ("The treaty's language was ______ enough that both nations later claimed it supported their position.", "vague", ["precise", "formal", "hostile"], "If both sides could claim it, the language was vague."),
            ("The young pianist's performance was technically flawless but emotionally ______, leaving the audience unmoved.", "flat", ["stirring", "intense", "generous"], "“Unmoved” indicates the performance lacked emotion."),
            ("Archaeologists ______ that the pottery was made locally, since its clay matches deposits found near the site.", "infer", ["deny", "doubt", "forget"], "Reasoning from evidence to a conclusion is inferring."),
            ("The park's rules are strictly ______: rangers issue fines to anyone who leaves a marked trail.", "enforced", ["ignored", "debated", "concealed"], "Issuing fines means the rules are enforced."),
            ("Her ______ remarks at the meeting, offered without preparation, nonetheless summarized the problem better than the written report.", "impromptu", ["rehearsed", "lengthy", "hostile"], "Offered without preparation means impromptu."),
            ("Coral reefs are ______ to small changes in water temperature; a rise of one degree can trigger bleaching.", "sensitive", ["immune", "indifferent", "resistant"], "A one-degree change triggering bleaching shows sensitivity."),
            ("The company's claim that the product is “completely natural” is ______, since the label lists three synthetic additives.", "misleading", ["accurate", "modest", "obvious"], "A claim contradicted by the label is misleading."),
            ("The manuscript survives only in a ______ copy; several pages are missing and others are water-damaged.", "fragmentary", ["complete", "pristine", "duplicate"], "Missing and damaged pages make the copy fragmentary."),
        ];
        for (var i = 0; i < bank.Length; i++)
        {
            var b = bank[i];
            yield return Mc(r, "WORDS_IN_CONTEXT", D(i, bank.Length), "Which choice completes the text with the most logical and precise word or phrase?", b.Correct, b.Wrong, b.Why + $" The best choice is <b>{b.Correct}</b>.", P(b.Sentence));
        }
    }

    // ---- Boundaries: punctuation between clauses ----
    private static IEnumerable<DemoQuestion> Boundaries(Random r)
    {
        (string Before, string After, string Correct, string[] Wrong, string Why)[] bank =
        [
            ("The research station sits at the edge of the ice shelf", "its instruments record wind speed every ten minutes.", ";", [",", "", " and,"], "Two independent clauses need a semicolon (or a period); a comma alone creates a comma splice."),
            ("Three crops dominate the valley's farms", "wheat, barley, and sunflowers.", ":", [";", ",", " —and"], "A colon introduces the list that explains the clause before it."),
            ("The committee reviewed the proposal", "which had been revised twice, and approved it without changes.", ",", [";", ":", ""], "A nonessential clause beginning with “which” is set off by commas."),
            ("Before the bridge opened in 1932", "residents crossed the river by ferry.", ",", [";", ":", ""], "An introductory dependent clause is followed by a comma."),
            ("The satellite's main instrument", "a wide-field camera, photographs the entire sky every three days.", ",", [";", ":", ""], "An appositive that renames the subject is set off by commas on both sides."),
            ("The team tested four prototypes", "only one of them survived the drop test.", ".", [",", "", " which,"], "These are two independent clauses; a period keeps them separate."),
            ("Visitors may enter the archive", "however, they must leave bags at the front desk.", ";", [",", "", ":"], "“However” joining two independent clauses needs a semicolon before it."),
            ("The cave paintings", "which were discovered in 1940, are about 17,000 years old.", ",", [";", "", ":"], "A nonessential “which” clause is enclosed in commas."),
            ("Students who finish early", "may begin the next section.", "", [",", ";", ":"], "The “who” clause is essential to the subject, so no punctuation separates it from the verb."),
            ("The museum closes at five", "the garden, though, stays open until dark.", ";", [",", "", ":"], "Two independent clauses require a semicolon."),
            ("She packed everything she needed for the expedition", "boots, a tent, a stove, and two weeks of food.", ":", [";", "", ","], "A colon introduces the explanatory list."),
            ("Although the storm had passed", "the roads remained closed for another day.", ",", [";", ":", ""], "An introductory clause with “although” takes a comma."),
            ("The recipe calls for one unusual ingredient", "smoked salt.", ":", [";", "", ","], "A colon introduces a single item that explains the preceding statement."),
            ("The orchestra's conductor", "Maria Chen, trained in Vienna.", ",", [";", ":", ""], "An appositive naming the conductor is set off by commas."),
            ("The experiment failed twice", "but the third attempt produced a clear result.", ",", [";", ":", ""], "A comma precedes the coordinating conjunction “but” that joins two independent clauses."),
            ("The first edition sold out within a week", "the publisher ordered a second printing immediately.", ";", [",", "", " so,"], "Independent clauses without a conjunction are joined by a semicolon."),
            ("The city's oldest building", "a stone granary from 1611, now houses a café.", ",", [";", ":", ""], "The appositive phrase is set off by commas."),
            ("Whenever the river floods", "the lower fields become a lake.", ",", [";", ":", ""], "An introductory “whenever” clause takes a comma."),
        ];
        for (var i = 0; i < bank.Length; i++)
        {
            var b = bank[i];
            var sentence = $"{b.Before}<b>______</b> {b.After}";
            string Show(string p) => p == "" ? "no punctuation" : p == " and," ? "and," : p == " which," ? "which," : p == " so," ? "so," : p == " —and" ? "— and" : p;
            var options = new List<string> { Show(b.Correct) };
            foreach (var w in b.Wrong) if (!options.Contains(Show(w))) options.Add(Show(w));
            var shuffled = options.OrderBy(_ => r.Next()).ToArray();
            yield return new DemoQuestion("BOUNDARIES", D(i, bank.Length), P(sentence), P("Which choice completes the text so that it conforms to the conventions of Standard English?"), shuffled, Array.IndexOf(shuffled, Show(b.Correct)), null, P(b.Why));
        }
    }

    // ---- Form, Structure, and Sense: agreement, tense, pronouns ----
    private static IEnumerable<DemoQuestion> FormStructureSense(Random r)
    {
        (string Before, string After, string Correct, string[] Wrong, string Why)[] bank =
        [
            ("The collection of rare maps ______ kept in a climate-controlled room.", "", "is", ["are", "were", "have been"], "The subject is the singular noun “collection,” so the verb is singular: is."),
            ("Each of the volunteers ______ assigned a section of the beach to clean.", "", "was", ["were", "have been", "are"], "“Each” is singular, so the verb is singular: was."),
            ("The data from the two surveys ______ that commuting times rose last year.", "", "suggest", ["suggests", "has suggested", "is suggesting"], "“Data” is plural here, so the plural verb “suggest” agrees."),
            ("By the time the rescue team arrived, the climbers ______ for the summit.", "", "had already left", ["have already left", "already leave", "are already leaving"], "An action completed before another past action takes the past perfect."),
            ("Neither the coach nor the players ______ satisfied with the result.", "", "were", ["was", "is", "has been"], "With “neither…nor,” the verb agrees with the nearer subject, the plural “players.”"),
            ("The engineer presented ______ findings to the council last night.", "", "her", ["their", "its", "she"], "A singular person referred to as “the engineer” takes a singular possessive pronoun."),
            ("Mangroves protect coastlines, filter water, and ______ young fish.", "", "shelter", ["sheltering", "to shelter", "sheltered"], "Items in a series must be parallel: protect, filter, shelter."),
            ("The number of applicants ______ doubled since the program was announced.", "", "has", ["have", "are", "were"], "“The number” is singular, so “has” agrees."),
            ("Every spring the cranes return to the marsh, where they ______ for several weeks before flying north.", "", "feed", ["feeds", "fed", "will have fed"], "The present-tense frame (“return”) calls for the present-tense plural “feed.”"),
            ("The committee published ______ report on Monday.", "", "its", ["their", "it's", "there"], "A committee acting as a single unit takes the singular possessive “its.”"),
            ("Running the length of the valley, ______.", "", "the river irrigates dozens of farms", ["dozens of farms are irrigated by the river", "farms are irrigated along the river", "irrigation by the river reaches dozens of farms"], "The modifier “Running the length of the valley” must describe the river, which must therefore be the subject."),
            ("The author's first two novels ______ out of print, but her third is still widely read.", "", "are", ["is", "has been", "was"], "The plural subject “novels” takes the plural verb “are.”"),
            ("Scientists hope that the new telescope ______ planets as small as Earth.", "", "will detect", ["detects", "detected", "has detected"], "“Hope that” about a future capability takes the future tense."),
            ("Students ______ complete the diagnostic test early may begin the practice set.", "", "who", ["which", "whom", "whose"], "“Who” is the subject pronoun for people."),
            ("One of the paintings ______ been attributed to a student of Rembrandt.", "", "has", ["have", "are", "were"], "“One” is the subject, so the verb is singular: has."),
            ("The documentary, along with its two sequels, ______ available online.", "", "is", ["are", "were", "have been"], "“Along with” does not make the subject plural; “documentary” is singular."),
            ("After reviewing the evidence, ______.", "", "the jury reached a verdict in two hours", ["a verdict was reached in two hours", "two hours were needed for a verdict", "the verdict took the jury two hours"], "The introductory phrase describes the jury, which must be the subject of the main clause."),
            ("If the glacier continues to retreat at the current rate, it ______ by 2080.", "", "will disappear", ["disappears", "disappeared", "had disappeared"], "A future consequence of a present condition takes “will.”"),
        ];
        for (var i = 0; i < bank.Length; i++)
        {
            var b = bank[i];
            var sentence = b.Before.Contains("______") ? b.Before.Replace("______", "<b>______</b>") : b.Before + " <b>______</b>" + b.After;
            yield return Mc(r, "FORM_STRUCTURE_SENSE", D(i, bank.Length), "Which choice completes the text so that it conforms to the conventions of Standard English?", b.Correct, b.Wrong, b.Why, P(sentence));
        }
    }

    // ---- Transitions ----
    private static IEnumerable<DemoQuestion> Transitions(Random r)
    {
        (string First, string Second, string Correct, string[] Wrong, string Why)[] bank =
        [
            ("Solar panels have become far cheaper over the past decade.", "______, many homeowners still hesitate because of the upfront cost.", "Nevertheless", ["Therefore", "For example", "Similarly"], "The second sentence contrasts with the first, so a contrast transition is needed."),
            ("The city planted ten thousand trees along its main avenues.", "______, summer street temperatures fell by nearly two degrees.", "As a result", ["However", "In contrast", "Meanwhile"], "The second sentence is a consequence of the first."),
            ("Some birds navigate using Earth's magnetic field.", "______, the European robin can sense magnetic direction with proteins in its eyes.", "For instance", ["Nonetheless", "Consequently", "Instead"], "The second sentence gives an example of the first."),
            ("The first draft of the treaty was rejected by both parliaments.", "______, negotiators returned to the table with a revised text.", "Consequently", ["Likewise", "For example", "In other words"], "The revised text is a result of the rejection."),
            ("The orchestra rehearsed the symphony for three months.", "______, the premiere received mixed reviews.", "Even so", ["Therefore", "In addition", "Specifically"], "A long rehearsal and mixed reviews are in tension; a concession transition fits."),
            ("Octopuses can change color in a fraction of a second.", "______, they can alter the texture of their skin to match rocks or coral.", "In addition", ["However", "Otherwise", "Thus"], "The second sentence adds a related ability."),
            ("The library's oldest manuscripts are too fragile to handle.", "______, researchers consult high-resolution scans.", "Instead", ["Similarly", "For example", "Furthermore"], "Scans replace handling the originals; “instead” signals the substitution."),
            ("The team's first rocket failed to reach orbit.", "______, its second launch, six months later, succeeded.", "However", ["Therefore", "In fact", "Likewise"], "Failure then success is a contrast."),
            ("Wool fibers trap air, which slows the loss of body heat.", "______, wool garments keep wearers warm even when damp.", "Accordingly", ["Conversely", "For example", "Meanwhile"], "The warmth is a consequence of the trapped air."),
            ("The novel was ignored when it appeared in 1922.", "______, it is now taught in universities around the world.", "Today, however", ["For this reason", "In the same way", "Specifically"], "Being ignored versus being widely taught is a contrast across time."),
            ("Researchers expected the drug to lower blood pressure.", "______, it also reduced inflammation.", "Unexpectedly", ["Therefore", "In short", "Likewise"], "The additional effect was not anticipated."),
            ("Electric buses cost more to buy than diesel buses.", "______, they are cheaper to fuel and maintain over their lifetime.", "On the other hand", ["As a result", "For example", "In conclusion"], "The two sentences weigh opposite considerations."),
        ];
        for (var i = 0; i < bank.Length; i++)
        {
            var b = bank[i];
            yield return Mc(r, "TRANSITIONS", D(i, bank.Length), "Which choice completes the text with the most logical transition?", b.Correct, b.Wrong, b.Why, P(b.First + " " + b.Second.Replace("______", "<b>______</b>")));
        }
    }

    // ---- Short original passages for reading skills ----
    private static IEnumerable<DemoQuestion> ShortPassages(Random r)
    {
        var bank = new (string Skill, Difficulty D, string Passage, string Question, string Correct, string[] Wrong, string Why)[]
        {
            ("CENTRAL_IDEAS", Difficulty.Easy, "Giant kelp off the California coast can grow as much as sixty centimeters in a single day, which lets kelp forests recover quickly after storms tear away their fronds. When sea urchin numbers explode, however, the urchins can eat young kelp faster than it regrows, turning a forest into bare seafloor within months.",
                "According to the text, what is one reason kelp forests recover quickly after storms?", "Kelp grows extremely fast.", ["Storms rarely damage kelp fronds.", "Sea urchins avoid damaged forests.", "Kelp fronds are too tough to tear."], "The text links fast growth directly to quick recovery."),
            ("CENTRAL_IDEAS", Difficulty.Medium, "When a young librarian took charge of a financier's private collection in 1905, she was one of very few women in the field. Over four decades she built it into a major research library, negotiating purchases of medieval manuscripts with a shrewdness dealers came to respect. Scholars today credit her, more than the financier, with shaping the library's identity.",
                "Which choice best states the main idea of the text?", "The librarian's expertise and negotiating skill were central to building the library.", ["The financier collected manuscripts mainly as an investment.", "Dealers were reluctant to sell to the library.", "The library specializes in medieval manuscripts."], "The passage centers on her decisive role."),
            ("CENTRAL_IDEAS", Difficulty.Hard, "Microplastics have been found in the deepest ocean trenches, yet their effects on deep-sea life are poorly understood. In one study, biologists fed small crustaceans diets containing plastic particles. The animals' growth slowed by about twenty percent, but, unexpectedly, their survival over eight weeks did not change.",
                "Which finding does the text describe as unexpected?", "The animals' survival was unaffected.", ["Microplastics were present in deep trenches.", "The animals consumed the particles.", "The animals' growth slowed."], "The word “unexpectedly” introduces the survival result."),
            ("CENTRAL_IDEAS", Difficulty.Easy, "Unlike most birds, the common swift spends almost its whole life in the air. It feeds on flying insects, drinks by skimming water, and even sleeps while gliding at high altitude. Swifts land only to nest.",
                "Based on the text, which activity does a swift perform on the ground?", "Nesting", ["Sleeping", "Drinking", "Feeding"], "Swifts land only to nest; everything else happens in flight."),
            ("CENTRAL_IDEAS", Difficulty.Medium, "A folklorist working in the rural South in the 1920s differed from her peers in one crucial respect: rather than posing as a detached scientist, she joined the storytelling sessions she recorded, trading tales with her informants. Some colleagues called the approach unscientific; later researchers recognized it as an early form of participant observation.",
                "Which choice best describes how the folklorist's fieldwork differed from that of her peers?", "She took part in the activities she documented instead of observing from a distance.", ["She worked in the rural South rather than in cities.", "She refused to record the stories she heard.", "She published her findings as fiction."], "The “one crucial respect” is participation rather than detachment."),
            ("INFERENCES", Difficulty.Medium, "Octopuses have no bones, and most of their neurons sit in their arms rather than in a central brain. Each arm can taste, touch, and make simple decisions. When researchers cut the nerve linking an arm to the brain, the arm still reached for and grasped nearby food. This suggests that ______",
                "Which choice most logically completes the text?", "much of an octopus's behavior is controlled locally by its arms.", ["octopuses cannot survive without a central brain.", "octopus arms are more sensitive to taste than to touch.", "an octopus's brain is larger than that of other mollusks."], "An arm acting while disconnected shows local control."),
            ("INFERENCES", Difficulty.Hard, "Medieval scribes copied manuscripts by hand, and errors crept in with each generation of copies. Scholars reconstructing an original therefore trust readings shared by copies that are otherwise unrelated. A reading found only in copies descended from the same damaged source, by contrast, ______",
                "Which choice most logically completes the text?", "is probably an error introduced by that source rather than the author's wording.", ["is likely to reflect the author's original wording.", "cannot be compared with other copies.", "should be preferred because the source is older."], "A reading confined to one family of copies likely originated in their shared source."),
            ("INFERENCES", Difficulty.Easy, "Desert tortoises dig burrows up to two meters deep, where the temperature stays far cooler than the surface. More than 350 other species, from owls to snakes, have been seen using these burrows. Conservationists therefore warn that declines in tortoise numbers ______",
                "Which choice most logically completes the text?", "could harm many species that depend on the burrows for shelter.", ["are unlikely to affect other desert animals.", "would be offset by increases in owls.", "are caused mainly by snakes."], "Fewer tortoises means fewer burrows for the hundreds of species that use them."),
            ("INFERENCES", Difficulty.Medium, "Ancient Roman concrete has survived two thousand years of seawater, while modern concrete crumbles within decades. Chemists recently found that the Roman mix contained lumps of lime that, when cracked by water, react to form new crystals that seal the crack. Engineers hoping to copy the effect ______",
                "Which choice most logically completes the text?", "may try adding reactive lime lumps so that small cracks repair themselves.", ["should remove all lime from their mixtures.", "will need to mix their concrete with seawater.", "must wait centuries to test their results."], "The self-healing came from reactive lime, so copying it means adding similar lumps."),
            ("INFERENCES", Difficulty.Hard, "Bilingual speakers often say they feel like different people in each language. Psychologists asked bilinguals to describe themselves twice, once in each language. The descriptions shifted subtly toward the cultural norms of each language, but scores on standard personality tests stayed the same. The findings suggest that ______",
                "Which choice most logically completes the text?", "the feeling of being a different person reflects self-presentation rather than underlying personality.", ["learning a second language changes a person's personality.", "personality tests are unreliable for bilingual speakers.", "the two languages encode personality differently."], "Presentation shifted while measured personality stayed stable."),
            ("TEXT_STRUCTURE", Difficulty.Medium, "Honeybees communicate the location of food through a “waggle dance.” The angle of the dance relative to the sun indicates direction, while its duration indicates distance. Researchers who filmed thousands of dances found that bees from one hive interpret these signals with striking consistency.",
                "Which choice best describes the function of the second sentence?", "It explains the mechanism named in the first sentence.", ["It introduces a counterargument to the first sentence.", "It describes the researchers' methods.", "It questions whether the dance works."], "The second sentence explains how the dance encodes information."),
            ("TEXT_STRUCTURE", Difficulty.Hard, "Historians have long dated the decline of a West African empire to the mid-fifteenth century, citing the loss of a major trading city. Yet newly translated tax records show that the empire's rulers kept collecting revenue from the gold-producing south for at least sixty more years. The empire, it seems, did not collapse so much as contract.",
                "Which choice best states the main purpose of the text?", "To challenge a conventional account of when and how an empire declined", ["To argue that the trading city was unimportant", "To describe how tax records were translated", "To explain why the city was lost"], "The text presents the old view, new evidence, and a reframing."),
            ("TEXT_STRUCTURE", Difficulty.Easy, "Few people associate Antarctica with plants, but the continent hosts two flowering species. Both survive on the warmer peninsula, where summer temperatures occasionally rise above freezing. As the peninsula warms, these plants have spread rapidly, a change biologists track as an indicator of regional climate.",
                "Which choice best describes the overall structure of the text?", "It presents a surprising fact, supports it with details, and notes its scientific significance.", ["It describes a problem, proposes a solution, and evaluates it.", "It compares two species and concludes one is better adapted.", "It summarizes a debate and takes a side."], "Surprising fact, supporting details, then significance."),
            ("COMMAND_OF_EVIDENCE", Difficulty.Medium, "Some researchers hypothesize that crows recognize individual human faces and remember people who have threatened them. To test this, a team wearing a distinctive “dangerous” mask captured and released several crows. Afterward, researchers walked around wearing either the dangerous mask or a neutral mask.",
                "Which finding, if true, would most directly support the hypothesis?", "Crows scolded and dove at people wearing the dangerous mask but ignored people in the neutral mask.", ["Crows were more numerous in the morning than in the afternoon.", "Crows scolded people in both masks equally often.", "Crows that had never been captured avoided all humans."], "Hostility aimed only at the dangerous mask shows recognition and memory."),
            ("COMMAND_OF_EVIDENCE", Difficulty.Hard, "An economic historian argues that one country's early industrialization was driven less by cheap coal than by a culture in which scientific findings were quickly shared with craftsmen and engineers.",
                "Which finding, if true, would most directly weaken the historian's argument?", "Several regions with abundant coal but little scientific exchange industrialized just as early.", ["Scientific societies published their proceedings in plain language.", "Many engineers of the period attended public science lectures.", "Coal prices rose steadily during the century."], "If coal-rich regions without knowledge-sharing industrialized just as early, culture was not decisive."),
            ("COMMAND_OF_EVIDENCE", Difficulty.Easy, "A student claims that a nineteenth-century poet wrote most of her nearly 1,800 poems during one brief, intensely productive period rather than steadily across her life.",
                "Which finding, if true, would most directly support the student's claim?", "Scholars date roughly 800 of the poems to a single four-year span.", ["The poet rarely left her family's home.", "Only ten of the poems were published in her lifetime.", "The poet bound many poems into small booklets."], "Nearly half the poems in four years supports a burst of productivity."),
            ("COMMAND_OF_EVIDENCE", Difficulty.Medium, "<b>Average commute time by mode, 2022</b><table><tr><th>Mode</th><th>Average minutes</th><th>Share of commuters</th></tr><tr><td>Car</td><td>31</td><td>62%</td></tr><tr><td>Bus</td><td>44</td><td>21%</td></tr><tr><td>Bicycle</td><td>22</td><td>9%</td></tr><tr><td>Walking</td><td>18</td><td>8%</td></tr></table>A planner claims that drivers spend more time commuting than users of any non-motorized mode but less time than bus riders.",
                "Which choice best describes data from the table that support the planner's claim?", "Car commutes average 31 minutes, longer than bicycle (22) and walking (18) but shorter than bus (44).", ["Cars carry 62% of commuters, more than all other modes combined.", "Bus commutes average 44 minutes, the longest of any mode.", "Walking commutes are shortest but used by only 8% of commuters."], "Only this choice makes all three comparisons in the claim."),
            ("CROSS_TEXT", Difficulty.Hard, "<b>Text 1</b><br>Some planners argue that widening roads reduces congestion by adding capacity, citing short-term studies in which travel times fell right after lanes were added.<br><br><b>Text 2</b><br>A transportation researcher analyzed decades of data and found that miles driven rise almost exactly in proportion to new road capacity. Within a few years, added lanes fill with trips that would not otherwise have been made.",
                "Based on the texts, how would the researcher in Text 2 most likely respond to the planners in Text 1?", "By arguing that the short-term reductions they cite disappear as new traffic is generated", ["By agreeing that widening roads is the best way to reduce congestion", "By suggesting that their studies measured the wrong cities", "By claiming that travel time is not a useful measure"], "The long-term finding undercuts the short-term improvements."),
            ("CROSS_TEXT", Difficulty.Medium, "<b>Text 1</b><br>A literary scholar contends that the sonnet's fixed fourteen lines limit what a poet can express and make the form ill-suited to modern subjects.<br><br><b>Text 2</b><br>A recent collection uses the fourteen-line form to address contemporary politics and personal grief. Critics praised how the form's constraints intensified rather than restricted the poems' emotional range.",
                "Based on the texts, the critics in Text 2 would most likely say that the scholar's claim in Text 1 is", "incomplete, because formal constraints can heighten rather than limit expression", ["correct, because the collection avoids modern subjects", "irrelevant, because the collection contains no sonnets", "unverifiable, because critics disagree"], "The critics found that constraints intensified expression on modern subjects."),
            ("RHETORICAL_SYNTHESIS", Difficulty.Medium, "While researching a topic, a student has taken the following notes:<ul><li>Bamboo is a grass that can grow nearly a meter in a day.</li><li>Its hollow stems are strong relative to their weight.</li><li>Bamboo scaffolding is still used on construction sites in several Asian cities.</li><li>It regrows from its roots after harvesting, so no replanting is needed.</li></ul>",
                "The student wants to emphasize why bamboo is a sustainable building material. Which choice most effectively uses relevant information from the notes to accomplish this goal?", "Because bamboo grows rapidly and regrows from its roots after harvesting, it can be used in construction without replanting.", ["Bamboo scaffolding is still used on construction sites in several Asian cities.", "Bamboo is a grass whose hollow stems are strong relative to their weight.", "Bamboo can grow nearly a meter in a day."], "Sustainability depends on rapid growth and regrowth without replanting."),
            ("RHETORICAL_SYNTHESIS", Difficulty.Easy, "While researching a topic, a student has taken the following notes:<ul><li>The Aral Sea was once the fourth-largest lake in the world.</li><li>Rivers feeding it were diverted for irrigation beginning in the 1960s.</li><li>By 2010 it had lost about 90 percent of its area.</li><li>A dam completed in 2005 has helped the northern part recover.</li></ul>",
                "The student wants to present the cause of the lake's shrinking. Which choice most effectively uses relevant information from the notes to accomplish this goal?", "The Aral Sea shrank because the rivers that fed it were diverted for irrigation starting in the 1960s.", ["The Aral Sea was once the fourth-largest lake in the world.", "By 2010 the Aral Sea had lost about 90 percent of its area.", "A dam completed in 2005 has helped the northern part recover."], "Only this choice states the cause."),
            ("RHETORICAL_SYNTHESIS", Difficulty.Hard, "While researching a topic, a student has taken the following notes:<ul><li>A 2021 trial gave 1,200 adults either a daily walking program or no program.</li><li>After a year, the walking group reported better sleep.</li><li>Blood pressure fell slightly in the walking group.</li><li>The study did not measure diet.</li></ul>",
                "The student wants to acknowledge a limitation of the study. Which choice most effectively uses relevant information from the notes to accomplish this goal?", "Although the walking group slept better and had lower blood pressure, the study did not measure diet, which could also explain the differences.", ["The 2021 trial included 1,200 adults.", "After a year, the walking group reported better sleep and slightly lower blood pressure.", "Walking programs improve sleep and lower blood pressure."], "The limitation is the unmeasured variable, diet."),
            ("RHETORICAL_SYNTHESIS", Difficulty.Medium, "While researching a topic, a student has taken the following notes:<ul><li>Lichens are partnerships between a fungus and an alga.</li><li>Some lichens grow less than a millimeter per year.</li><li>Scientists use lichen size to estimate how long a rock surface has been exposed.</li><li>The method is called lichenometry.</li></ul>",
                "The student wants to explain how lichens are used for dating. Which choice most effectively uses relevant information from the notes to accomplish this goal?", "Because some lichens grow at a known, very slow rate, scientists can estimate how long a rock has been exposed by measuring lichen size, a method called lichenometry.", ["Lichens are partnerships between a fungus and an alga.", "Some lichens grow less than a millimeter per year.", "Lichenometry is a method scientists use."], "This choice connects the slow growth rate to the dating method."),
        };
        foreach (var b in bank)
            yield return Mc(r, b.Skill, b.D, b.Question, b.Correct, b.Wrong, b.Why, P(b.Passage));
    }
}
