using Article = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededArticle;
using Chapter = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededChapter;
using Step = NOOSE_Website.Infrastructure.Handbook.HandbookContent.SeededStep;

namespace NOOSE_Website.Infrastructure.Handbook.Content;

/// <summary>Chapter eight: the service regulation in short form, paragraph references kept.</summary>
internal static class DutyRegulationChapter
{
    internal static readonly Chapter Chapter = new(
        "kap-dienstvorschrift", "dienstvorschrift", "Dienstvorschrift",
        "Die Dienstverordnung in Kurzform: Freigaben, Abteilungen, Beförderung, Sanktionen und Regeldienst.",
        "Policy",
        [
            new Article("art-dvo-dienstverordnung", "dienstverordnung", "Die Dienstverordnung",
                "Die verbindliche Grundlage des Dienstes - und was dieses Kapitel daraus macht.",
                """
                <p>Die <strong>Dienstverordnung</strong> legt fest, unter welchen Bedingungen du Dienst tust:
                wer was darf, wie du auftrittst, was du tragen und fahren darfst, und was geschieht, wenn du
                dich nicht daran hältst. Sie ist keine Empfehlung - Verstöße ziehen dienstrechtliche Folgen bis
                zur sofortigen Entbindung vom Dienst nach sich.</p>
                <p>Dieses Kapitel ist die <strong>Kurzfassung</strong>. Es steht hier, damit du eine Regel in
                demselben Werkzeug nachschlagen kannst, in dem du arbeitest - über die Handbuchsuche und die
                Erklär-Blasen des Glossars. Der vollständige Text liegt als Dokument in der
                <em>Dokumenten-Bibliothek</em>.</p>
                <p>Die Paragrafen sind überall mitangegeben (§2.4.1 und so weiter), damit du jede Regel im
                vollen Text wiederfindest.</p>
                <p>Widersprechen sich Kurzfassung und Dienstverordnung, <strong>gilt die
                Dienstverordnung</strong>. Melde den Widerspruch, statt dich auf das Handbuch zu berufen.</p>
                """,
                RoleplayHtml:
                """
                <p>Fehlende Passagen sind laut Vorwort „mit Sinn und Verstand zu Ende zu führen“. Das ist kein
                Freibrief, sondern die Aufforderung, im Zweifel nachzufragen - für unbekannte Einrichtungen und
                Abläufe macht §9.9 die Rückfrage bei der Führungsebene ausdrücklich zur Pflicht.</p>
                """),

            new Article("art-dvo-freigaben", "sicherheitsfreigaben", "Sicherheitsfreigaben",
                "Die vier Stufen der Dienstverordnung - und wie sie sich zu den VS-Stufen dieser Seite verhalten.",
                """
                <p>Der Zugang zu sicherheitsrelevanten Informationen läuft über
                <strong>Sicherheitsfreigaben</strong>. Sie werden nach zwei Prinzipien zugleich vergeben (§1.1):
                dem <strong>Rangprinzip</strong> - was dein Dienstgrad mitbringt - und dem
                <strong>Fachprinzip</strong> - was dein Einsatzbereich nötig macht. Das Fachprinzip kann eine
                Freigabe situationsbedingt anheben.</p>
                <p>Es gibt vier Stufen:</p>
                <ul>
                <li><strong>Confidential</strong> - die Grundstufe für interne Informationen, für sämtliche
                Special Agenten.</li>
                <li><strong>Secret</strong> - operative Inhalte, laufende Maßnahmen, alle Lageberichte und
                sensible Ermittlungsansätze. Nach dem Rangprinzip beim Supervisory Special Agent.</li>
                <li><strong>Top Secret</strong> - Stufe 5, den höchsten Dienstgraden vorbehalten: Director und
                Deputy Director.</li>
                <li><strong>Special Access Program</strong> - Stufe 6 und keine gewöhnliche Freigabe, sondern
                eine Zusatzstufe für Ermittlungen, die über die reguläre Zuständigkeit der
                Generalstaatsanwaltschaft hinausgehen und nochmals abgeschirmt behandelt werden.</li>
                </ul>
                <p><strong>Diese Leiter ist nicht die VS-Stufe dieser Seite.</strong> Die Seite kennt
                <em>Offen</em>, <em>Führung</em>, <em>TRU</em> und <em>HRB</em>. Das sind vier Sichtbarkeiten,
                keine Freigabestufen: die Dienstverordnung staffelt nach Rang, die Seite nach Rang
                <em>und</em> Abteilung.</p>
                <p>Als Faustregel: <em>Führung</em> auf der Seite deckt ungefähr Secret und darüber ab,
                <em>TRU</em> und <em>HRB</em> sind das Fachprinzip in Reinform. Für das <em>Special Access
                Program</em> gibt es auf der Seite absichtlich keine Entsprechung - solche Vorgänge gehören
                nicht in eine Akte, die jemand versehentlich öffnet.</p>
                """,
                RoleplayHtml:
                """
                <p>Zwei Stellen der Dienstverordnung lassen sich schwer zusammenbringen: §3.3 erklärt
                <em>alle</em> Informationen, an die du nur durch deine Stellung gelangst, pauschal zur
                Geheimhaltungsstufe 5 „Top Secret“ - §1.1 beschreibt Confidential zugleich als Grundstufe für
                interne Informationen. Bis das geklärt ist, gilt nach außen die strengere Lesart: nichts
                verlässt die Behörde ohne Freigabe.</p>
                """,
                DiagramKey: "sicherheitsfreigaben"),

            new Article("art-dvo-dienstgrade", "dienstgrade", "Dienstgrade",
                "Die sechs Grade und was die Dienstverordnung an jeden von ihnen knüpft.",
                """
                <p>Die Behörde kennt sechs Dienstgrade (§2.1). Sie sind nicht bloß eine Reihenfolge - an jedem
                hängen Befugnisse, Freigaben und Ausrüstung:</p>
                <table>
                <thead><tr><th>Dienstgrad</th><th>Was daran hängt</th></tr></thead>
                <tbody>
                <tr><td><strong>Junior Agent</strong></td><td>Einstieg. Grundeinweisung und Tarnausbildung vor
                dem ersten Regeldienst, dazu die Fortbildung „(Erweiterte) Verhörmethoden“</td></tr>
                <tr><td><strong>Special Agent</strong></td><td>Zugang zu TRU und HRB. Pump Shotgun und
                Dienstgewehr. Darf Besucher in den Hauptsitz lassen (§9.1)</td></tr>
                <tr><td><strong>Senior Special Agent</strong></td><td>Darf die Sanktionsstufen 1 und 2
                verhängen. Spezialkarabiner, Schwere Schrotflinte, Scharfschützengewehr und Tränengas. Auf der
                Seite: höchste Einstufung vergeben und selbst veröffentlichen</td></tr>
                <tr><td><strong>Supervisory Special Agent</strong></td><td>Beginn der Führung, Freigabe Secret,
                Sanktionsstufen 3 bis 6. Kampfgewehr. Zutritt zur Harbor Container Facility. Von den
                Beschränkungen der Einsatzbegleitung ausgenommen</td></tr>
                <tr><td><strong>Deputy Director</strong></td><td>Freigabe Top Secret. Auf der Seite: entscheidet
                über Beförderungen</td></tr>
                <tr><td><strong>Director</strong></td><td>Höchster Dienstgrad, Freigabe Top Secret</td></tr>
                </tbody>
                </table>
                <p>Der Behördenleitung steht es jederzeit frei, einen beliebigen Agenten in einen beliebigen
                Dienstgrad zu setzen (§2.4.4) - die Kriterien der Beförderung binden sie nicht.</p>
                """),

            new Article("art-dvo-abteilungen", "abteilungen", "CID, TRU und HRB",
                "Zwei operative Abteilungen, eine administrative - und wer hinein darf.",
                """
                <p>Die Behörde hat drei Abteilungen (§2.2). Sie schließen einander nicht aus: TRU und HRB
                werden <strong>parallel</strong> zur Zugehörigkeit zur CID ausgeübt.</p>
                <ul>
                <li><strong>Criminal Investigative Division (CID)</strong> - die Hauptdirektion und die
                grundlegende Einheit für die Ermittlungsarbeit. Hier ist jeder Agent zuhause.</li>
                <li><strong>Tactical Response Unit (TRU)</strong> - eine ergänzende Spezialisierung, die
                zusätzliche operative und taktische Aufgabenbereiche eröffnet.</li>
                <li><strong>Human Resource Branch (HRB)</strong> - die administrative Unterstützung in
                Personalangelegenheiten: Bewerbungen vor- und nachbereiten, Bewerber betreuen,
                Sicherheitsüberprüfungen durchführen, Fort- und Weiterbildungen anbieten und mögliche
                Beförderungen prüfen.</li>
                </ul>
                <p><strong>Der Beitritt zu TRU und HRB ist erst ab dem Dienstgrad Special Agent möglich</strong>
                (§2.4.1), und er setzt die jeweilige Ausbildung voraus. Die Seite hält sich daran: unterhalb von
                Special Agent lässt sich das Kennzeichen gar nicht erst setzen.</p>
                <p>Die HRB hat ein <strong>Mitgliederlimit von fünf Personen</strong>. Wegen der wenigen Plätze
                wird dort hohe Aktivität erwartet; wer sie nicht erbringt, muss mit dem Ausschluss aus der HRB
                rechnen. Dieses Limit zählt die Seite nicht mit - darauf achtet die Führung selbst.</p>
                """),

            new Article("art-dvo-ausbildungen", "aus-und-fortbildungen", "Aus- und Fortbildungen",
                "Was du absolviert haben musst - und in welcher Reihenfolge.",
                """
                <p>Vor dem erstmaligen Antritt des Regeldienstes sind <strong>Grundeinweisung</strong> und
                <strong>Tarnausbildung</strong> erfolgreich zu absolvieren (§2.3). Ohne sie beginnt der Dienst
                nicht.</p>
                <p>Im Dienstgrad des Junior Agent kommt die Fortbildung <strong>„(Erweiterte)
                Verhörmethoden“</strong> hinzu. Ihr Abschluss ist Voraussetzung für die Beförderung zum Special
                Agent.</p>
                <p>Die <strong>Drohnenausbildung</strong> berechtigt zur Verwendung der Aufklärungsdrohne und ist
                für die Beförderung zum Special Agent verpflichtend. Wer die Drohne ohne diese Berechtigung oder
                entgegen den Bestimmungen einsetzt, begeht eine Dienstpflichtverletzung.</p>
                <p>Ab dem Dienstgrad Special Agent stehen die beiden Spezialisierungen offen: die
                <strong>Spezialisierungsausbildung der Tactical Response Unit</strong> und die
                <strong>Ausbildung für die Human Resource Branch</strong>. Erst sie machen dich zum Teil der
                jeweiligen Einheit.</p>
                <p>Auf der Seite werden diese Ausbildungen als <strong>Ausbildungsmodule</strong> in der
                Personalakte geführt und einzeln abgehakt. Die Liste der Module wird nicht mitgeliefert - die
                Führung legt sie unter <em>Einstellungen</em> an, damit sie zur jeweils geltenden Fassung der
                Dienstverordnung passt.</p>
                """),

            new Article("art-dvo-befoerderung", "befoerderungskriterien", "Beförderungskriterien",
                "Was die Dienstverordnung für jeden Schritt nach oben verlangt.",
                """
                <p>Das Verfahren - Antrag, Entscheidung, neue Anmeldung - steht im Kapitel
                <em>Personal &amp; Führung</em>. Hier stehen die <strong>Voraussetzungen</strong>, die die
                Dienstverordnung in §2.4 an jeden Schritt knüpft.</p>
                <p><strong>Zum Special Agent</strong> (§2.4.1): abgeschlossene Drohnenausbildung, dazu gute
                Aktivität, saubere Aktenarbeit und einschlägige Ermittlungsergebnisse. Ab hier ist der Beitritt
                zu TRU und HRB möglich.</p>
                <p><strong>Zum Senior Special Agent</strong> (§2.4.2): vorbildliche Aktivität, makellose
                Aktenarbeit, lückenlose Dokumentation von Geschehnissen und nachweisbare Führungsqualitäten.
                Zwingend sind außerdem eine <strong>aktive Zugehörigkeit zu TRU oder HRB</strong> und eine
                <strong>abgeschlossene Infiltration</strong>. Auch wer alles erfüllt, hat keinen Anspruch: die
                Posten sind begrenzt.</p>
                <p><strong>Zum Supervisory Special Agent</strong> (§2.4.3): Dieser Dienstgrad ist die Leitung
                einer Spezialisierung. Die entsprechende Leitungsposition muss frei sein, und vorausgesetzt
                werden unentbehrliche Dienste für die Spezialisierung sowie das vollste Vertrauen der
                Behördenleitung.</p>
                <p><strong>Außerordentlich</strong> (§2.4.4): Der Behördenleitung steht es jederzeit frei, einen
                beliebigen Agenten in einen beliebigen Dienstgrad zu setzen. Deshalb prüft die Seite bei einem
                Beförderungsantrag auch nicht, ob der Zielgrad der nächsthöhere ist.</p>
                """),

            new Article("art-dvo-verhalten", "verhalten-im-dienst", "Verhalten im Dienst",
                "Auftreten, Kollegen, Verschwiegenheit - und das Gebot des Eigenschutzes.",
                """
                <p><strong>Auftreten</strong> (§3.1): Als Repräsentant der Behörde trittst du gegenüber allen
                Personen äußerst respektvoll, freundlich und selbstsicher auf. Erwartet wird ein seriöses und
                gepflegtes Erscheinungsbild.</p>
                <p><strong>Kollegen</strong> (§3.2): Jedem Kollegen ist stets mit Respekt entgegenzutreten -
                unabhängig von Dienstalter und Dienstgrad.</p>
                <p><strong>Verschwiegenheit</strong> (§3.3): Alle Informationen, an die du allein durch deine
                Stellung gelangst, sind nur für den Dienstgebrauch bestimmt und dürfen unter keinen Umständen an
                unbeteiligte Dritte weitergegeben werden. Ausgenommen ist die durch Dienstanweisung vorgesehene
                Zusammenarbeit mit anderen staatlichen Behörden.</p>
                <p><strong>Öffentliche Stellungnahmen</strong> (§3.4): Im Namen der Behörde spricht
                ausschließlich die Behördenleitung oder wer von ihr ausdrücklich befugt wurde. Das gilt auch für
                alles, was du auf den öffentlichen Seiten dieser Anwendung veröffentlichst.</p>
                <p><strong>Waffen und Asservate</strong> (§3.5): Sichergestellte Waffen, waffenähnliche
                Gegenstände und sonstige Asservate sind unverzüglich der Asservatenkammer zuzuführen -
                Zwischenlagerungen sind unzulässig. Jedes Asservat ist zudem umgehend auf dieser Seite
                einzutragen.</p>
                <p><strong>Eigenschutz</strong> (§4.1): Deine eigene Sicherheit und die deiner Kollegen ist das
                oberste Gut. Handlungen, die sie fahrlässig oder vorsätzlich riskieren, sind strengstens
                untersagt.</p>
                """),

            new Article("art-dvo-weisung", "weisungsbefugnis", "Weisungsbefugnis",
                "Wer wem etwas sagen darf - und wann der Dienstgrad nicht zählt.",
                """
                <p>Die <strong>Weisungsbefugnis</strong> richtet sich grundsätzlich nach der Rangordnung der
                Dienstgrade (§3.6). Dienstliche Anordnungen sind entsprechend verbindlich umzusetzen.</p>
                <p>Übergeordnet steht das <strong>Parlamentspräsidium</strong>: der Vorsitz des Bundesrates und
                der Speaker; in dessen Abwesenheit der stellvertretende Speaker.</p>
                <p>Es gibt eine wichtige Ausnahme. In besonderen Einsatzkonstellationen - vor allem in einer
                eingesetzten <strong>Taskforce</strong> - ist die jeweils ernannte Einsatz- oder Gesamtleitung
                weisungsbefugt. Diese Befugnis gilt <strong>unabhängig vom regulären Dienstgrad</strong> der
                Beteiligten und für die Dauer der Maßnahme.</p>
                <p>Im Klartext: In einer Taskforce kann ein Special Agent einem Deputy Director Anweisungen
                geben, wenn er die Einsatzleitung hat. Die Seite bildet das nicht ab - sie kennt nur Dienstgrade
                und Zuteilungen. Wer die Leitung hat, gehört deshalb in die Beschreibung der Taskforce.</p>
                """),

            new Article("art-dvo-sanktionen", "sanktionsstufen", "Sanktionsstufen",
                "Sechs Stufen, jede mit eigener Schwelle - und wo sie festgehalten werden.",
                """
                <p>Disziplinarmaßnahmen richten sich nach §27 des Landesbeamtengesetzes und werden nach Schwere
                des Verstoßes gestuft (§3.7). Jede Stufe darf erst ab einem bestimmten Dienstgrad verhängt
                werden:</p>
                <table>
                <thead><tr><th>Stufe</th><th>Maßnahme</th><th>Zulässig ab</th></tr></thead>
                <tbody>
                <tr><td>1</td><td>Mündlicher oder schriftlicher Verweis bei geringfügigen oder erstmaligen
                Verstößen; Eintrag als neutraler oder negativer Vermerk in die Personalakte</td><td>Senior
                Special Agent</td></tr>
                <tr><td>2</td><td>Ermahnung, Verwarnung oder Schulungsverpflichtung bei wiederholten oder
                schwerwiegenden Verstößen</td><td>Senior Special Agent</td></tr>
                <tr><td>3</td><td>Entzug von Sonderrechten oder Befugnissen, etwa Ausrüstungs- oder
                Fahrzeugfreigaben</td><td>Supervisory Special Agent</td></tr>
                <tr><td>4</td><td>Temporäre Suspendierung bis zur abschließenden Klärung des
                Sachverhalts</td><td>Supervisory Special Agent</td></tr>
                <tr><td>5</td><td>Degradierung, also Herabstufung im Dienstgrad</td><td>Supervisory Special
                Agent</td></tr>
                <tr><td>6</td><td>Entlassung: sofortige und dauerhafte Entbindung vom Dienst</td><td>Supervisory
                Special Agent</td></tr>
                </tbody>
                </table>
                <p>Vor jeder Maßnahme hat der betroffene Agent das <strong>Recht auf Stellungnahme</strong>. Bei
                schwerwiegendem Fehlverhalten, das die Behörde oder ihre Mitglieder unmittelbar gefährdet, ist
                eine vorläufige Suspendierung ohne vorherige Anhörung zulässig (§3.7.1).</p>
                <p>Festgehalten wird das in der <strong>Personalakte</strong>, im Abschnitt <em>Vermerke</em>.
                Ein Verweis ist dort ein Vermerk der Art <em>Negativer Vermerk</em>; die Bezeichnung lässt sich
                überschreiben, wenn <em>Verweis</em> oder <em>Ermahnung</em> genauer trifft.</p>
                """),

            new Article("art-dvo-regeldienst", "regeldienst", "Ermittlung, Undercover, Infiltration",
                "Der Kern des Dienstes - und wo die Genehmigungspflicht beginnt.",
                """
                <p>Die <strong>Ermittlung</strong> ist der Kern des Regeldienstes (§4.2): Zielpersonen werden
                identifiziert, beobachtet und analysiert, Informationen systematisch ausgewertet, Beweismittel
                gesichert und operative Maßnahmen vorbereitet.</p>
                <p><strong>Taskforces</strong> (§4.3) werden bei komplexen Verfahren oder besonderen
                Gefährdungslagen eingerichtet. Die Führungsebene wählt die Mitglieder nach Fachkompetenz aus; die
                Taskforce arbeitet auf einen klar umrissenen Auftrag hin und wird danach aufgelöst oder in den
                regulären Ermittlungsdienst überführt.</p>
                <p><strong>Undercover-Tätigkeiten</strong> (§4.4) gehören zum regulären Ermittlungsdienst und
                können von <em>jedem ausgebildeten Agenten</em> im Rahmen seiner Befugnisse durchgeführt werden.
                Sie dienen der verdeckten Informationsgewinnung, der unauffälligen Beobachtung und der diskreten
                Kontaktaufnahme.</p>
                <p>Die <strong>Infiltration</strong> (§4.5) ist etwas anderes: das verdeckte Eindringen in
                kriminelle oder sicherheitsrelevante Strukturen zur langfristigen Informationsgewinnung. Sie
                erfolgt <strong>ausschließlich nach interner Genehmigung</strong> und unter strenger
                Risikoabwägung. Eine abgeschlossene Infiltration ist zugleich Voraussetzung für die Beförderung
                zum Senior Special Agent.</p>
                """,
                RoleplayHtml:
                """
                <p>Der Unterschied zwischen Undercover und Infiltration ist Dauer und Tiefe. Wer sich einmal
                unerkannt umhört, ist undercover. Wer sich in eine Struktur hineinarbeitet und dort eine Rolle
                einnimmt, infiltriert - und braucht dafür vorher eine Genehmigung.</p>
                """),

            new Article("art-dvo-zusammenarbeit", "behoerdliche-zusammenarbeit",
                "Zusammenarbeit mit anderen Behörden",
                "Koordinationsrecht, operative Unabhängigkeit - und wann ein Einsatzbericht fällig wird.",
                """
                <p>Die Behörde arbeitet mit sämtlichen staatlichen Stellen zusammen und pflegt einen
                lagebezogenen Informationsaustausch unter Beachtung der Geheimhaltungsstufen (§4.6). Bei
                Sachverhalten mit Einfluss auf die nationale Sicherheit steht ihr das
                <strong>Koordinationsrecht</strong> gegenüber allen Behörden zu; betroffene Stellen sind dann zur
                Zusammenarbeit verpflichtet.</p>
                <p><strong>Operative Unabhängigkeit</strong> (§4.6.1): Agenten nehmen operative Tätigkeiten
                grundsätzlich eigenständig wahr. Gemeinsame Streifen-, Transport- oder Einsatzfahrten mit
                Angehörigen anderer Behörden finden nicht statt. Ausnahmen ordnet allein die Führungsebene an -
                allgemein für bestimmte Lagen oder im Einzelfall für namentlich benannte Agenten.</p>
                <p><strong>Einsatzlagen anderer Behörden</strong> (§4.6.2): Die eigenständige und unaufgeforderte
                Begleitung fremder Einsatzlagen, insbesondere des Los Santos Police Department, ist untersagt.
                Kurzfristig anfahren darfst du sie nur, um mögliche Zielpersonen, Observationsobjekte oder andere
                Ermittlungsansätze festzustellen, die in die Zuständigkeit der Behörde fallen. Ergibt sich kein
                konkreter dienstlicher Bezug, verlässt du den Ort unverzüglich.</p>
                <p>Ausdrücklich untersagt ist die Begleitung aus persönlichem Interesse, zur bloßen Beobachtung,
                aus mangelnder eigener Auslastung oder bei Lagen mit geringem Gefährdungspotenzial - etwa
                ATM-Raube oder gewöhnliche Fahrzeugverfolgungen.</p>
                <p>Nimmst du dennoch an einer fremden Einsatzlage teil, ist darüber <strong>unverzüglich ein
                Einsatzbericht auf dieser Seite anzufertigen</strong>, nach der behördeninternen Vorlage. Das ist
                eine <em>Operation</em> im Bereich <em>Ermittlung</em>. Ausgenommen von dieser Pflicht und von
                den Beschränkungen sind die Behördenleitung und die Führungsebene ab Supervisory Special
                Agent.</p>
                <p>Die Missachtung dieser Vorschrift ist ein schwerer Dienstverstoß und wird nach §3.7 geahndet;
                wiederholte oder besonders schwere Verstöße können die sofortige Entlassung nach sich
                ziehen.</p>
                """,
                Steps:
                [
                    new Step("Visibility", "Dienstlichen Bezug prüfen",
                        "Gibt es eine Zielperson, ein Observationsobjekt, einen Ermittlungsansatz? Wenn nicht, wieder abfahren."),
                    new Step("Assignment", "Operation anlegen",
                        "Ermittlung → Operationen → Neu. Ort, Zeit und die eigene Beteiligung eintragen."),
                    new Step("Description", "Ergebnis nachtragen",
                        "Was war die Lage, was hast du festgestellt, was folgt daraus für unsere Akten."),
                ]),
        ]);
}
