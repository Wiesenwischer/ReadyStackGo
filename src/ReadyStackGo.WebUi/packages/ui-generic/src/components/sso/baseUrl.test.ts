import { describe, expect, it } from "vitest";
import { normalizeBaseUrl, satisfiesHttpsRequirement, suggestBaseUrl } from "./baseUrl";

describe("suggestBaseUrl", () => {
  it("uses the browser origin without trailing slash", () => {
    expect(suggestBaseUrl("https://rsgo.example.com")).toBe("https://rsgo.example.com");
    expect(suggestBaseUrl("https://rsgo.example.com/")).toBe("https://rsgo.example.com");
  });
});

describe("normalizeBaseUrl", () => {
  it("keeps valid addresses and removes the trailing slash", () => {
    expect(normalizeBaseUrl(" https://rsgo.example.com/ ")).toBe("https://rsgo.example.com");
    expect(normalizeBaseUrl("http://server:8080")).toBe("http://server:8080");
    expect(normalizeBaseUrl("https://example.com/rsgo/")).toBe("https://example.com/rsgo");
  });

  it("rejects invalid addresses", () => {
    expect(normalizeBaseUrl("")).toBeNull();
    expect(normalizeBaseUrl("rsgo.example.com")).toBeNull();
    expect(normalizeBaseUrl("ftp://rsgo.example.com")).toBeNull();
    expect(normalizeBaseUrl("https://rsgo.example.com/?a=1")).toBeNull();
    expect(normalizeBaseUrl("https://rsgo.example.com/#x")).toBeNull();
    expect(normalizeBaseUrl("https://user:pw@rsgo.example.com")).toBeNull();
  });
});

describe("satisfiesHttpsRequirement", () => {
  it("accepts https and loopback http", () => {
    expect(satisfiesHttpsRequirement("https://rsgo.example.com")).toBe(true);
    expect(satisfiesHttpsRequirement("http://localhost:8080")).toBe(true);
    expect(satisfiesHttpsRequirement("http://127.0.0.1:8080")).toBe(true);
    expect(satisfiesHttpsRequirement("http://127.1.2.3")).toBe(true);
    expect(satisfiesHttpsRequirement("http://[::1]:8080")).toBe(true);
  });

  it("rejects http on other hosts, including private ones", () => {
    expect(satisfiesHttpsRequirement("http://server:8080")).toBe(false);
    expect(satisfiesHttpsRequirement("http://192.168.1.10")).toBe(false);
    expect(satisfiesHttpsRequirement("http://localhost.example.com")).toBe(false);
    expect(satisfiesHttpsRequirement("not a url")).toBe(false);
  });
});
