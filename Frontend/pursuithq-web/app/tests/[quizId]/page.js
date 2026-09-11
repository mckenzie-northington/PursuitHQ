'use client';

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { quizzes as quizzesApi } from "@/lib/api";
import { useAuth } from "@/components/AuthProvider";

// Matches QuestionType in the API.
const MULTIPLE_CHOICE = 0;
const TRUE_FALSE = 1;
const SHORT_ANSWER = 2;

export default function TakeTestPage() {
  const { quizId } = useParams();
  const id = Number(quizId);
  const { user, loading } = useAuth();

  const [quiz, setQuiz] = useState(null);
  const [answers, setAnswers] = useState({});
  const [result, setResult] = useState(null);

  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    try {
      setQuiz(await quizzesApi.get(id));
      setError("");
    } catch (err) {
      setError(err.message);
    } finally {
      setReady(true);
    }
  }, [id]);

  useEffect(() => {
    if (loading || !user) return;
    load();
  }, [loading, user, load]);

  async function submit(e) {
    e.preventDefault();

    const unanswered = quiz.questions.filter((q) => !(answers[q.id] ?? "").trim()).length;

    if (unanswered > 0) {
      const word = unanswered === 1 ? "question" : "questions";
      if (!confirm(`${unanswered} ${word} left blank. Submit anyway?`)) return;
    }

    setSubmitting(true);
    setError("");

    try {
      const marked = await quizzesApi.submit(
        id,
        quiz.questions.map((q) => ({ questionId: q.id, answer: answers[q.id] ?? "" }))
      );

      setResult(marked);
      window.scrollTo({ top: 0, behavior: "smooth" });
    } catch (err) {
      setError(err.message);
    } finally {
      setSubmitting(false);
    }
  }

  function retake() {
    setResult(null);
    setAnswers({});
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  if (loading || !ready) {
    return <div className="mx-auto max-w-3xl px-6 py-10 text-slate-500">Loading...</div>;
  }

  if (!quiz) {
    return (
      <div className="mx-auto max-w-3xl px-6 py-10">
        <p className="text-slate-600">{error || "That test could not be loaded."}</p>
        <Link href="/courses" className="mt-3 inline-block text-indigo-600 hover:underline">
          Back to courses
        </Link>
      </div>
    );
  }

  const written = quiz.questions.filter((q) => q.questionType === SHORT_ANSWER).length;

  return (
    <div className="mx-auto max-w-3xl px-6 py-10">
      {quiz.courseId && (
        <Link
          href={`/courses/${quiz.courseId}/tests`}
          className="text-sm text-indigo-600 hover:underline"
        >
          &larr; {quiz.courseName || "Practice tests"}
        </Link>
      )}

      <h1 className="mt-2 text-2xl font-semibold">{quiz.title}</h1>
      <p className="mt-1 text-sm text-slate-600">
        {quiz.questions.length} questions
        {written > 0 && ` · ${written} graded on meaning by AI`}
      </p>

      {error && (
        <div className="mt-4 flex items-start justify-between gap-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          <span>{error}</span>
          <button onClick={() => setError("")} className="shrink-0 font-medium">
            Dismiss
          </button>
        </div>
      )}

      {result ? (
        <Results result={result} onRetake={retake} courseId={quiz.courseId} />
      ) : (
        <form onSubmit={submit} className="mt-6 space-y-4">
          {quiz.questions.map((question, index) => (
            <Question
              key={question.id}
              question={question}
              index={index}
              value={answers[question.id] ?? ""}
              onChange={(value) => setAnswers((a) => ({ ...a, [question.id]: value }))}
            />
          ))}

          <div className="flex items-center gap-3 pt-2">
            <button
              type="submit"
              disabled={submitting}
              className="rounded-md bg-indigo-600 px-5 py-2.5 text-sm font-medium text-white transition hover:bg-indigo-700 disabled:opacity-50"
            >
              {submitting ? "Marking..." : "Submit"}
            </button>
            {submitting && written > 0 && (
              <span className="text-sm text-slate-500">Reading your written answers...</span>
            )}
          </div>
        </form>
      )}
    </div>
  );
}

function Question({ question, index, value, onChange }) {
  const name = `q-${question.id}`;

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-5">
      <p className="font-medium text-slate-900">
        <span className="mr-2 text-slate-400">{index + 1}.</span>
        {question.questionText}
      </p>

      {question.questionType === MULTIPLE_CHOICE && (
        <div className="mt-3 space-y-1.5">
          {question.options.map((option) => (
            <label
              key={option}
              className="flex cursor-pointer items-start gap-2.5 rounded-md px-2 py-1.5 text-sm text-slate-700 transition hover:bg-slate-50"
            >
              <input
                type="radio"
                name={name}
                checked={value === option}
                onChange={() => onChange(option)}
                className="mt-0.5 h-4 w-4 border-slate-300 text-indigo-600"
              />
              <span>{option}</span>
            </label>
          ))}
        </div>
      )}

      {question.questionType === TRUE_FALSE && (
        <div className="mt-3 flex gap-2">
          {["true", "false"].map((option) => (
            <button
              key={option}
              type="button"
              onClick={() => onChange(option)}
              className={`rounded-md border px-4 py-1.5 text-sm font-medium capitalize transition ${
                value === option
                  ? "border-indigo-600 bg-indigo-600 text-white"
                  : "border-slate-300 text-slate-700 hover:bg-slate-50"
              }`}
            >
              {option}
            </button>
          ))}
        </div>
      )}

      {question.questionType === SHORT_ANSWER && (
        <textarea
          rows={3}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="Answer in a sentence or two..."
          className="mt-3 w-full rounded-md border border-slate-300 px-3 py-2 text-sm outline-none focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500"
        />
      )}
    </div>
  );
}

function Results({ result, onRetake, courseId }) {
  const tone =
    result.score >= 80 ? "text-green-600" : result.score >= 60 ? "text-amber-600" : "text-red-600";

  return (
    <div className="mt-6">
      <div className="rounded-xl border border-slate-200 bg-white px-6 py-8 text-center">
        <p className={`text-4xl font-semibold ${tone}`}>{result.score}%</p>
        <p className="mt-2 text-slate-600">
          {result.correctCount} of {result.totalCount} correct
        </p>

        <div className="mt-5 flex justify-center gap-2">
          <button
            onClick={onRetake}
            className="rounded-md bg-indigo-600 px-4 py-2 text-sm font-medium text-white transition hover:bg-indigo-700"
          >
            Take it again
          </button>
          {courseId && (
            <Link
              href={`/courses/${courseId}/tests`}
              className="rounded-md border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-50"
            >
              All tests
            </Link>
          )}
        </div>
      </div>

      <div className="mt-4 space-y-3">
        {result.questions.map((q, index) => (
          <div
            key={q.questionId}
            className={`rounded-xl border bg-white p-5 ${
              q.isCorrect ? "border-green-200" : "border-red-200"
            }`}
          >
            <div className="flex items-start gap-2">
              <span
                className={`mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-xs font-semibold text-white ${
                  q.isCorrect ? "bg-green-600" : "bg-red-500"
                }`}
              >
                {q.isCorrect ? "✓" : "✕"}
              </span>
              <p className="font-medium text-slate-900">
                <span className="mr-1.5 text-slate-400">{index + 1}.</span>
                {q.questionText}
              </p>
            </div>

            <dl className="mt-3 space-y-2 pl-7 text-sm">
              <div>
                <dt className="text-xs font-medium uppercase tracking-wide text-slate-400">
                  Your answer
                </dt>
                <dd className={`whitespace-pre-wrap ${q.givenAnswer ? "text-slate-700" : "italic text-slate-400"}`}>
                  {q.givenAnswer || "Left blank"}
                </dd>
              </div>

              {!q.isCorrect && (
                <div>
                  <dt className="text-xs font-medium uppercase tracking-wide text-slate-400">
                    {q.questionType === SHORT_ANSWER ? "Model answer" : "Correct answer"}
                  </dt>
                  <dd className="whitespace-pre-wrap text-slate-700">{q.correctAnswer}</dd>
                </div>
              )}

              {q.feedback && (
                <p className="rounded-md bg-slate-50 px-3 py-2 text-slate-600">{q.feedback}</p>
              )}

              {q.explanation && (
                <p className="text-slate-500">{q.explanation}</p>
              )}
            </dl>
          </div>
        ))}
      </div>
    </div>
  );
}
