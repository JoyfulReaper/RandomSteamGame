
export async function getAnalyticsOverview(days) {
    const response = await fetch(
        `/api/admin/analytics/overview?days=${encodeURIComponent(days)}`,
        {
            method: "GET",
            credentials: "same-origin",
            cache: "no-store",
            headers: {
                Accept: "application/json"
            }
        }
    );

    if (!response.ok) {
        throw new Error("Analytics request failed.");
    }

    return await response.json();
}
