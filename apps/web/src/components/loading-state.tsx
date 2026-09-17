export function LoadingState() {
  return (
    <main className="shell loading-state" aria-busy="true" aria-label="Loading archive">
      <div className="loading-title shimmer" />
      <div className="loading-layout">
        <div className="loading-filter shimmer" />
        <div className="loading-cards">
          {Array.from({ length: 3 }, (_, index) => (
            <div className="loading-card shimmer" key={index} />
          ))}
        </div>
      </div>
    </main>
  );
}

