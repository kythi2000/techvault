"use client";

import { useEffect } from "react";

export default function ErrorPage({
  error,
  retry,
}: {
  error: Error & { digest?: string };
  retry: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main className="shell status-page" role="alert">
      <span className="status-code">UNEXPECTED DISPLAY ERROR</span>
      <h1>The exhibit lights went out.</h1>
      <p>This page could not be displayed. Please try again.</p>
      <button className="button button-dark" type="button" onClick={retry}>Try again</button>
    </main>
  );
}
