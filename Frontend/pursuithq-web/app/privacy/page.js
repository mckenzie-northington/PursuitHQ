/*
  CONTACT ADDRESS: support@pursuit-hq.com below is a placeholder. Replace it
  with a real, monitored mailbox before anyone outside the project uses this
  app - a privacy policy that points at an address nobody reads is worse than
  no address at all. The same placeholder is in app/terms/page.js.
*/

/*
  STATUS OF THIS PAGE

  This is a starting draft. It was written from what the code in this repo
  actually does - what is stored, which third parties it is sent to, what the
  account-deletion path removes - and not from a template.

  It is not legal advice, and it was not written by a lawyer. Before real users
  rely on it, have somebody qualified read it. Requirements differ depending on
  where your users are (FERPA, GDPR, state privacy laws), and this draft makes
  no attempt to guess which apply to you.

  Keep it true. If a feature changes what is collected or where it is sent,
  this page changes in the same commit.
*/

export const metadata = {
  title: "Privacy Policy",
  description:
    "What PursuitHQ stores, who else it is sent to, and what you can change or delete.",
};

const LAST_UPDATED = "17 September 2026";

const SECTIONS = [
  { id: "who", title: "Who runs PursuitHQ" },
  { id: "you-give", title: "What you tell us" },
  { id: "you-make", title: "What the app stores as you use it" },
  { id: "ai", title: "AI features and Google - please read this one" },
  { id: "third-parties", title: "The other companies involved" },
  { id: "not-done", title: "What PursuitHQ does not do" },
  { id: "others-see", title: "What other students can see" },
  { id: "your-choices", title: "Your choices, and one honest gap" },
  { id: "security", title: "How your information is protected" },
  { id: "age", title: "Age" },
  { id: "changes", title: "Changes to this page" },
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

export default function PrivacyPage() {
  const n = (id) => SECTIONS.findIndex((s) => s.id === id) + 1;

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      <h1 className="text-2xl font-semibold">Privacy Policy</h1>
      <p className="mt-1 text-sm text-slate-600">Last updated {LAST_UPDATED}</p>

      <section className={`mt-6 ${card}`}>
        <div className="space-y-3 text-sm leading-relaxed text-slate-700">
          <p>
            PursuitHQ is a student project, not a company. This page says what the
            app stores, who else ends up holding it, and what you can change or
            delete. It is written to be read rather than to sound impressive, so
            it is in plain English and it is short where it can be.
          </p>
          <p>
            One part matters more than the rest. If you use the AI features, what
            you send goes to Google, and on the plan PursuitHQ is currently on,
            Google is allowed to use it to improve their products. That is{" "}
            <a href="#ai" className="font-medium text-indigo-600 hover:text-indigo-700">
              section {n("ai")}
            </a>
            . Read it before you paste a resume.
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
        <Section id="who" number={n("who")} title="Who runs PursuitHQ">
          <p>
            PursuitHQ is built and maintained by one college student. There is no
            company behind it, no staff, and no support desk. Where this page says
            &quot;we&quot;, it means that one person.
          </p>
          <p>
            This page covers the PursuitHQ website and the API behind it. It does
            not cover anything else you might link to from inside the app.
          </p>
        </Section>

        <Section id="you-give" number={n("you-give")} title="What you tell us">
          <p>To create an account, you give:</p>
          <Bullets
            items={[
              "Your first and last name.",
              "Your email address, which is also how you sign in.",
              "A password. It is stored only as a PBKDF2 hash, through ASP.NET Identity. The password you typed is never written down anywhere, so nobody - including whoever is running the app - can read it back.",
              "Your time zone, so that due dates and reminder times mean what you expect.",
              "The date the account was created.",
            ]}
          />
          <p>
            Everything on your profile beyond that is optional and starts empty:
            your school, your major, your graduation year, your education level
            (not set, high school, college, university, or other) and a profile
            photo. You can leave all of it blank and the app still works.
          </p>
        </Section>

        <Section
          id="you-make"
          number={n("you-make")}
          title="What the app stores as you use it"
        >
          <p>
            PursuitHQ is a place to keep your own coursework and career material,
            so most of what it stores is simply what you put in it.
          </p>
          <p className="font-medium text-slate-900">Coursework</p>
          <Bullets
            items={[
              "Courses and class schedules.",
              "Assignments, including due dates and any grades you record.",
              "Calendar events and notes.",
              "Study materials you upload: PDF, Word, PowerPoint, Excel, plain text and images.",
            ]}
          />
          <p className="font-medium text-slate-900">Study tools</p>
          <Bullets
            items={[
              "Flashcard decks.",
              "Quizzes, and your attempts at them.",
              "Study guides.",
              "Study-chat conversations.",
              "Study sessions.",
            ]}
          />
          <p className="font-medium text-slate-900">Career</p>
          <Bullets
            items={[
              "Resumes, including anything you type into them.",
              "Goals, skills and certifications.",
              "Job postings you save.",
            ]}
          />
          <p className="font-medium text-slate-900">People</p>
          <Bullets
            items={[
              "Connection requests, and any note you attach to one.",
              "Direct messages and group messages.",
              "Reactions to messages, and any file you attach to a message.",
              "Group names, descriptions and photos.",
            ]}
          />
          <p className="font-medium text-slate-900">Two live signals</p>
          <p>
            Inside a conversation, the app shares how far you have read (read
            receipts) and whether you are typing right now. Other people in that
            conversation see both. There is no hidden way to read a message
            without the sender knowing.
          </p>
          <p className="font-medium text-slate-900">Settings</p>
          <p>Your notification preferences, which you can change at any time.</p>
        </Section>

        <Section
          id="ai"
          number={n("ai")}
          title="AI features and Google - please read this one"
        >
          <p>
            Four features send your content to Google&apos;s Gemini API: resume
            review, flashcard generation, quiz and study-guide generation, and
            study chat. Your data goes to Google through those features and no
            other part of the app.
          </p>

          <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3 text-sm leading-relaxed text-amber-800">
            <p className="font-medium">
              PursuitHQ uses Gemini&apos;s free tier. On the free tier, Google uses
              the content that is submitted to it to improve their products.
            </p>
            <p className="mt-2">
              That includes the text of a resume you ask to have reviewed, and the
              study material you ask to be turned into flashcards, a quiz or a
              study guide. Once it has been sent, it is out of PursuitHQ&apos;s
              hands. Deleting it here does not pull it back.
            </p>
          </div>

          <p className="font-medium text-slate-900">What that means in practice</p>
          <p>
            Do not put anything into an AI feature that you would mind a large
            company keeping a copy of. A resume with your home address and phone
            number on it is the common case, so strip those before you ask for a
            review - they are not what the review is about anyway. The same goes
            for an employer&apos;s internal documents, a professor&apos;s
            unpublished material, or anything covered by an agreement you signed.
          </p>
          <p>
            Skipping the AI features costs you nothing else. Courses, assignments,
            the calendar, messages and everything in section {n("you-make")} work
            without them.
          </p>
          <p className="font-medium text-slate-900">What is never sent</p>
          <p>
            A prompt contains only the data of the student who asked for it. No
            other student&apos;s work, messages, profile or files ever go into
            your prompt, and yours never go into theirs.
          </p>
        </Section>

        <Section
          id="third-parties"
          number={n("third-parties")}
          title="The other companies involved"
        >
          <p>
            Running a web app means other companies hold your data on the
            way through. These are all of them:
          </p>
          <Bullets
            items={[
              "Google (Gemini) - the AI features, as described in section " + n("ai") + ".",
              "Resend - sends the email the app sends you: assignment reminders, notifications about messages and connection requests, and password resets. Resend therefore handles your email address and the text of those emails.",
              "Neon - hosts the PostgreSQL database, which means almost everything in sections " + n("you-give") + " and " + n("you-make") + " sits on their servers.",
              "Cloudflare R2 - stores uploaded files: study materials, message attachments and profile photos.",
              "Vercel - hosts the website.",
              "Render - hosts the API.",
            ]}
          />
          <p>
            Each has its own privacy terms, and each is used as infrastructure:
            they store or move your data so the app can work. None of them is
            handed your data for their own advertising or marketing.
          </p>
        </Section>

        <Section
          id="not-done"
          number={n("not-done")}
          title="What PursuitHQ does not do"
        >
          <p>
            These are worth stating, because they are not what you would expect
            from most apps:
          </p>
          <Bullets
            items={[
              "There is no advertising anywhere in PursuitHQ.",
              "Your data is not sold, rented, or shared with anyone for marketing. There is no arrangement of that kind and no plan for one.",
              "There are no analytics, no tracking pixels, no third-party embeds and no advertising cookies. Nobody is following you around the web on PursuitHQ's behalf.",
              "There is no cookie banner, because there is nothing to consent to. The only things kept in your browser are the token that keeps you signed in and your choice of light or dark theme.",
              "Notification emails tell you who messaged you and which group it was in. They never quote the message. The content of what somebody sent you stays inside the app, where it is behind your password.",
              "Images you upload are re-encoded on the server. That strips EXIF metadata, including the GPS coordinates some phones quietly attach to photos.",
              "Uploaded files are never put in a public folder. Every download goes through an endpoint that checks who you are first, so knowing a file's address is not enough to open it.",
            ]}
          />
        </Section>

        <Section
          id="others-see"
          number={n("others-see")}
          title="What other students can see"
        >
          <p className="font-medium text-slate-900">
            You are not findable until you say so
          </p>
          <p>
            Being listed in the student directory is off by default. Until you
            turn on &quot;Let other students find me&quot; in Settings, searching
            for you turns up nothing. When you do turn it on, a classmate
            searching sees your name, your school and your year. Your email
            address is never shown.
          </p>
          <p className="font-medium text-slate-900">Inside a conversation</p>
          <p>
            The people in a conversation with you see your messages, anything you
            attach, your reactions, how far you have read, and when you are
            typing. Group members see the group name, description and photo.
          </p>
          <p className="font-medium text-slate-900">Connection requests</p>
          <p>
            A note attached to a connection request is shown to the person you
            sent it to.
          </p>
          <p className="font-medium text-slate-900">Blocking</p>
          <p>
            You can block another student. Use it; it is there for exactly the
            reason you think.
          </p>
        </Section>

        <Section
          id="your-choices"
          number={n("your-choices")}
          title="Your choices, and one honest gap"
        >
          <p className="font-medium text-slate-900">Delete your account</p>
          <p>
            Deleting your account removes your rows from the database and the
            files stored for you: study materials, message attachments and your
            profile photo. It is permanent, and there is no undo, so take a copy
            of anything you want to keep first.
          </p>
          <p className="font-medium text-slate-900">Change things in Settings</p>
          <Bullets
            items={[
              "What the app emails you about, or whether it emails you at all.",
              "Your time zone.",
              "Whether other students can find you.",
              "Your password, and two-step verification.",
            ]}
          />
          <p className="font-medium text-slate-900">Getting a copy of your data</p>
          <p>
            There is no export feature yet. That is a real gap rather than a
            position, and it is worth saying plainly instead of burying. Until it
            exists, email{" "}
            <a
              href="mailto:support@pursuit-hq.com"
              className="font-medium text-indigo-600 hover:text-indigo-700"
            >
              support@pursuit-hq.com
            </a>{" "}
            and the request will be put together by hand. One person does that by
            hand, so allow some days for it.
          </p>
        </Section>

        <Section
          id="security"
          number={n("security")}
          title="How your information is protected"
        >
          <Bullets
            items={[
              "Passwords are hashed with PBKDF2 through ASP.NET Identity and never stored in plain text.",
              "You can turn on two-step verification with an authenticator app, in Settings. If you do one thing from this list, do that one.",
              "Every database query is scoped to the signed-in student, so guessing at an id does not return somebody else's rows. Reading a conversation requires being an active member of it.",
              "Sign-in and registration are rate limited, which slows down anyone trying passwords in bulk.",
            ]}
          />
          <p className="font-medium text-slate-900">The honest part</p>
          <p>
            No system is perfectly secure, and it would be dishonest to write this
            section as though this one were. PursuitHQ is a student project
            maintained by one person. There is no security team, no audit and
            nobody on call at three in the morning.
          </p>
          <p>
            So: use a password you do not use anywhere else, turn on two-step
            verification, and do not keep anything here that would seriously hurt
            you if it got out.
          </p>
        </Section>

        <Section id="age" number={n("age")} title="Age">
          <p>
            You have to be at least 16 to have a PursuitHQ account. If an account
            turns out to belong to somebody younger, it will be removed along with
            the data attached to it.
          </p>
        </Section>

        <Section id="changes" number={n("changes")} title="Changes to this page">
          <p>
            PursuitHQ is still being built, and what it collects will change as
            features are added. When that happens, this page is updated in the
            same breath and the date at the top changes with it. It is worth a
            look now and then.
          </p>
        </Section>

        <Section id="contact" number={n("contact")} title="Contact">
          <p>
            Questions about any of this, a request for your data, or something on
            this page that does not match what you see the app doing:{" "}
            <a
              href="mailto:support@pursuit-hq.com"
              className="font-medium text-indigo-600 hover:text-indigo-700"
            >
              support@pursuit-hq.com
            </a>
            . If you have found something that looks like a security problem,
            please say so in the subject line.
          </p>
        </Section>
      </div>

      <p className="mt-6 text-sm text-slate-600">
        See also the{" "}
        <a href="/terms" className="font-medium text-indigo-600 hover:text-indigo-700">
          Terms of Service
        </a>
        .
      </p>
    </div>
  );
}
