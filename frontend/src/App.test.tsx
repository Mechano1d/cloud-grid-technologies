import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import App from "./App";

afterEach(() => {
  localStorage.clear();
  vi.restoreAllMocks();
});

describe("authentication screen", () => {
  it("shows a clear message when the API rejects login credentials", async () => {
    vi.spyOn(globalThis, "fetch").mockResolvedValue(
      new Response(
        JSON.stringify({ status: 401, title: "Unauthorized", detail: "Invalid email or password." }),
        { status: 401, headers: { "Content-Type": "application/problem+json" } },
      ),
    );
    render(<App />);

    fireEvent.change(screen.getByLabelText("Email"), { target: { value: "user@example.com" } });
    fireEvent.change(screen.getByLabelText("Пароль"), { target: { value: "wrongpass" } });
    fireEvent.click(screen.getByRole("button", { name: "Увійти в кабінет" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Invalid email or password.");
    await waitFor(() => expect(screen.getByLabelText("Email")).toHaveValue("user@example.com"));
  });
});
