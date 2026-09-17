/*
  CONTACT ADDRESS: support@pursuit-hq.com below is a placeholder. Replace it
  with a real, monitored mailbox before anyone outside the project uses this
  app - these terms tell people to report abuse to that address, and a report
  that lands nowhere is worse than no report button at all. The same
  placeholder is in app/privacy/page.js.
*/

/*
  STATUS OF THIS PAGE

  This is a starting draft, written from what the code in this repo actually
  does - the reporting paths that exist, what account deletion removes, what
  the app does and does not promise.

  It is not legal advice, and it was not written by a lawyer. Before real users
  rely on it, have somebody qualified read it. What a disclaimer of warranty or
  a limitation of liability is worth depends on where you and your users are,
  and this draft does not try to guess.

  Keep it true. If the rules or the enforcement tools change, this page changes
  in the same commit.
*/

export const metadata = {
  title: "Terms of Service",
  description:
    "The rules for using PursuitHQ, in plain English: who can join, what you may post, and what the app does not promise.",
};

const LAST_UPDATED = "17 September 2026";

const SECTIONS = [
  { id: "what", title: "What PursuitHQ is" },
  { id: "who", title: "Who can use it" },
  { id: "account", title: "Your account" },
  { id: "your-content", title: "What you post and send" },
  { id: "rules", title: "Things you must not do" },
  { id: "reporting", title: "Reporting, and what happens next" },
  { id: "ownership", title: "Your work stays yours" },
  { id: "ai", title: "The AI features" },
  { id: "as-is", title: "This is provided as-is" },
  { id: "backups", title: "Keep your own copy" },
  { id: "closing", title: "Closing your account" },
  { id: "changes", title: "Changes to these terms" },
  { id: "contact", title: "Contact" },
];

const card = "rounded-xl border border-slate-200 bg-white p-5";

/** One numbered section. The number comes from the list above, so the page and
 *  its contents can never drift apart. */
function Section({ id, number, title, children }) {
  return (
    <section id={id} className={`scroll-mt-24 ${card}`}>
      <h2 className="font-medium text-slate-900">
        <span className="text-slate-400">{number}.</span> {title}
      </h2>
      <div className="mt-3 space-y-3 text-sm leading-relaxed text-slate-700">
        {children}
      </div>
    </section>
  );
}

function Bullets({ items }) {
  return (
    <ul className="list-disc space-y-1.5 pl-5">
      {items.map((item, i) => (
        <li key={i}>{item}</li>
      ))}
    </ul>
  );
}

export default function TermsPage() {
  const n = (id) => SECTIONS.findIndex((s) => s.id === id) + 1;

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Terms of Service</h1>
      <p className="mt-1 text-sm text-slate-600">Last updated {LAST_UPDATED}</p>

      <section className={`mt-6 ${card}`}>
        <div className="space-y-3 text-sm leading-relaxed text-slate-700">
          <p>
            These are the rules for using PursuitHQ. Using the app means you
            accept them. They are written in plain English because rules nobody
            reads are not rules.
          </p>
          <p>
            The short version: be at least 16, do not be cruel to other people
            here, keep your own backups of anything that matters, and understand
            that this is one student&apos;s project rather than a service with
            guarantees behind it.
          </p>
        </div>
      </section>

      <nav aria-label="Contents" className={`mt-6 ${card}`}>
        <h2 className="font-medium text-slate-900">Contents</h2>
        <ol className="mt-3 space-y-1.5 text-sm">
          {SECTIONS.map((section, i) => (
            <li key={section.id}>
              <a
                href={`#${section.id}`}
                className="text-slate-700 transition hover:text-indigo-700"
              >
                <span className="text-slate-400">{i + 1}.</span> {section.title}
              </a>
            </li>
          ))}
        </ol>
      </nav>

      <div className="mt-6 space-y-6">
        <Section id="what" number={n("what")} title="What PursuitHQ is">
          <p>
            PursuitHQ is a place for students to keep track of courses,
            assignments, study material, career goals and the people they know.
            It is built and run by one college student. There is no company
            behind it and no support team.
          </p>
          <p>
            It is free to use. Nothing in it is a paid service, and nothing here
            is a contract for one.
          </p>
        </Section>

        <Section id="who" number={n("who")} title="Who can use it">
          <p>
            You must be at least 16 years old to create an account. A check for
            this is being added to registration; in the meantime, the rule still
            applies. If an account turns out to belong to somebody under 16, it
            will be removed.
          </p>
          <p>
            One account per person. Do not create an account for somebody else or
            in somebody else&apos;s name.
          </p>
        </Section>

        <Section id="account" number={n("account")} title="Your account">
          <Bullets
            items={[
              "Give a real name and a real email address. Other students see your name, and the email address is how you get back in if you are locked out.",
              "Your account is yours. Do not share your password or let somebody else sign in as you.",
              "Use a password you do not use anywhere else, and turn on two-step verification in Settings.",
              "If you think somebody else has got into your account, change your password and email the address in section " + n("contact") + ".",
            ]}
          />
        </Section>

        <Section
          id="your-content"
          number={n("your-content")}
          title="What you post and send"
        >
          <p>
            You are responsible for what you put into PursuitHQ and for what you
            send to other people through it. That covers messages, attachments,
            group names and descriptions, notes on connection requests, your
            profile, and anything you upload.
          </p>
          <p>
            Only upload things you have the right to upload. Course material that
            you were told not to share, somebody else&apos;s work, or a file you
            do not have permission to pass on does not belong here.
          </p>
          <p>
            A message you send to another student is something you have handed to
            them. Assume you cannot take it back.
          </p>
        </Section>

        <Section id="rules" number={n("rules")} title="Things you must not do">
          <p>Not on PursuitHQ, for any reason:</p>
          <Bullets
            items={[
              "Harassment. Repeatedly contacting somebody who does not want to hear from you, or setting out to make somebody miserable.",
              "Threats or encouraging violence, including against yourself.",
              "Hate speech - attacking people over race, ethnicity, national origin, religion, disability, sex, gender identity or sexual orientation.",
              "Sexual content involving minors, of any kind, in any form. This is reported, not just removed.",
              "Spam: bulk messages, unwanted promotion, chain messages.",
              "Impersonating another person, a school, a company or PursuitHQ itself.",
              "Posting somebody else's private information - their address, phone number, student ID, or anything else they did not choose to make public.",
              "Trying to reach accounts, conversations, files or data that are not yours, whether by guessing, probing, or exploiting a bug rather than reporting it.",
              "Uploading malware, or anything designed to damage a device or an account.",
              "Deliberately overloading or disrupting the service.",
            ]}
          />
          <p>
            If you find a security hole, email it rather than using it. That is
            genuinely helpful and it will be treated as such.
          </p>
        </Section>

        <Section
          id="reporting"
          number={n("reporting")}
          title="Reporting, and what happens next"
        >
          <p>
            There is a report button on messages and on student profiles. Use it.
            A report goes to the person who runs PursuitHQ, who can remove content
            and suspend or remove accounts.
          </p>
          <p>
            Be realistic about the timing: one person reads those reports, around
            classes. It is not a moderation team and there is no round-the-clock
            queue. If something is urgent, or involves a threat to somebody&apos;s
            safety, contact your school or the police as well - they can act and
            this app cannot.
          </p>
          <p>
            You can also block another student, which does not require anybody to
            review anything first.
          </p>
          <p>
            Breaking the rules in section {n("rules")} can get your content removed
            and your account suspended or deleted, depending on what happened.
            Serious cases do not get a warning first.
          </p>
        </Section>

        <Section
          id="ownership"
          number={n("ownership")}
          title="Your work stays yours"
        >
          <p>
            You keep ownership of everything you upload and write. Your notes,
            your resumes, your study material and your messages are yours.
          </p>
          <p>
            By putting something into PursuitHQ, you grant only the permission the
            app needs to do its job: to store your content, and to show it back to
            you and to the people you sent it to. Nothing more. It is not used to
            promote the app, it is not shown to anyone you did not send it to, and
            it is not sold or licensed to anybody.
          </p>
        </Section>

        <Section id="ai" number={n("ai")} title="The AI features">
          <p>
            Resume review, flashcard and quiz and study-guide generation, and
            study chat send your content to Google&apos;s Gemini API. On the plan
            PursuitHQ uses, Google is allowed to use submitted content to improve
            their products.
          </p>
          <p>
            Using those features means accepting that. Do not put anything into
            them that you would mind a large company keeping. The{" "}
            <a
              href="/privacy#ai"
              className="font-medium text-indigo-600 hover:text-indigo-700"
            >
              Privacy Policy
            </a>{" "}
            explains this in full.
          </p>
          <p>
            What the AI produces is a suggestion, not an answer. A resume review is
            an opinion, generated text can be confidently wrong, and a quiz it
            wrote can contain mistakes. Check it before you rely on it, and do not
            treat any of it as career, academic, legal or financial advice.
          </p>
        </Section>

        <Section id="as-is" number={n("as-is")} title="This is provided as-is">
          <p>
            PursuitHQ is provided as it is, with no warranty of any kind. There is
            no promise that it will be available, that it will work correctly,
            that reminders will arrive, or that your data will still be there
            tomorrow.
          </p>
          <p>
            It is a student project under active development. It will change.
            Features will be added, changed and removed. It may break. It may be
            taken offline permanently, possibly with little notice, because the
            person running it graduated or the hosting bill came due.
          </p>
          <p>
            To the extent the law allows, the person who runs PursuitHQ is not
            liable for loss or damage that comes from using it - including lost
            data, a missed deadline, or a reminder that never arrived.
          </p>
        </Section>

        <Section id="backups" number={n("backups")} title="Keep your own copy">
          <p>
            Do not let PursuitHQ be the only copy of anything you care about.
            Keep your resume somewhere else. Keep your real assignment deadlines
            in whatever your school uses. Keep the files you uploaded on your own
            computer.
          </p>
          <p>
            This is the single most practical line on the page. There are no
            guaranteed backups for you here, and there is no export feature yet.
          </p>
        </Section>

        <Section id="closing" number={n("closing")} title="Closing your account">
          <p>
            You can delete your account at any time from Settings. Doing so
            removes your rows from the database and the files stored for you:
            study materials, message attachments and your profile photo.
          </p>
          <p>
            It is permanent and there is no undo. Take copies of what you want
            first.
          </p>
          <p>
            One thing to understand: messages you already sent to other people
            were delivered to them. Deleting your account removes your side, but
            it cannot reach into someone else&apos;s conversation and unsend what
            they already received.
          </p>
          <p>
            An account may also be closed from this end for breaking the rules in
            section {n("rules")}, or if PursuitHQ is shut down.
          </p>
        </Section>

        <Section id="changes" number={n("changes")} title="Changes to these terms">
          <p>
            These terms will change as the app does. When they do, the date at the
            top changes with them. Continuing to use PursuitHQ after a change
            means you accept the new version, so it is worth a look now and then.
          </p>
          <p>
            If a change is one you are not willing to accept, you can delete your
            account as described in section {n("closing")}.
          </p>
        </Section>

        <Section id="contact" number={n("contact")} title="Contact">
          <p>
            Questions about these terms, a report that needs a human, or a
            security problem you would rather send privately:{" "}
            <a
              href="mailto:support@pursuit-hq.com"
              className="font-medium text-indigo-600 hover:text-indigo-700"
            >
              support@pursuit-hq.com
            </a>
            .
          </p>
        </Section>
      </div>

      <p className="mt-6 text-sm text-slate-600">
        See also the{" "}
        <a href="/privacy" className="font-medium text-indigo-600 hover:text-indigo-700">
          Privacy Policy
        </a>
        .
      </p>
    </div>
  );
}
