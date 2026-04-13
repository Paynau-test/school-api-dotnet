#!/usr/bin/env node
/**
 * Generates Postman collections for school-api-dotnet (Scores API).
 * Run: node scripts/generate-postman.js
 * Or:  make postman
 */

import { writeFileSync, mkdirSync } from "fs";

// ── Endpoints definition ─────────────────

const endpoints = [
  { name: "Login", path: "/login", method: "POST", public: true },
  { name: "Health Check", path: "/health", method: "GET", public: true },
  { name: "Get Scores", path: "/scores", method: "GET", public: false },
  { name: "Record Score", path: "/scores", method: "POST", public: false },
];

// ── Helpers ──────────────────────────────

function getBody(method, path) {
  if (method === "POST" && path === "/login") {
    return {
      mode: "raw",
      raw: JSON.stringify(
        { email: "admin@school.com", password: "password123" },
        null,
        2
      ),
    };
  }
  if (method === "POST" && path === "/scores") {
    return {
      mode: "raw",
      raw: JSON.stringify(
        {
          studentId: 1,
          subjectId: 1,
          gradeId: 1,
          year: 2026,
          month: 4,
          score: 9.5,
        },
        null,
        2
      ),
    };
  }
  return undefined;
}

function getQuery(method, path) {
  if (method === "GET" && path === "/scores") {
    return [
      { key: "studentId", value: "1", description: "Student ID (required)" },
      { key: "gradeId", value: "1", description: "Grade ID (required)" },
      { key: "year", value: "2026", description: "Year (required)" },
      { key: "month", value: "4", description: "Month 1-12 (required)" },
    ];
  }
  return undefined;
}

function buildRequest(ep) {
  const headers = [];

  if (ep.method === "POST") {
    headers.push({ key: "Content-Type", value: "application/json" });
  }
  if (!ep.public) {
    headers.push({
      key: "Authorization",
      value: "Bearer {{token}}",
      type: "text",
    });
  }

  const request = {
    method: ep.method,
    header: headers,
    url: {
      raw: `{{base_url}}${ep.path}`,
      host: ["{{base_url}}"],
      path: ep.path.split("/").filter(Boolean),
    },
  };

  const body = getBody(ep.method, ep.path);
  if (body) request.body = body;

  const query = getQuery(ep.method, ep.path);
  if (query) request.url.query = query;

  return request;
}

function loginTestScript() {
  return [
    {
      listen: "test",
      script: {
        type: "text/javascript",
        exec: [
          "const res = pm.response.json();",
          'if (res.success && res.data && res.data.token) {',
          '    pm.collectionVariables.set("token", res.data.token);',
          '    console.log("Token saved to collection variable");',
          "}",
        ],
      },
    },
  ];
}

// ── Build collection ─────────────────────

function buildCollection(envName, baseUrl, postmanId) {
  const authEndpoints = endpoints.filter((ep) => ep.path === "/login");
  const scoreEndpoints = endpoints.filter((ep) => ep.path === "/scores");
  const healthEndpoints = endpoints.filter((ep) => ep.path === "/health");

  return {
    info: {
      name: `School API .NET (Scores) - ${envName}`,
      _postman_id: postmanId,
      description: `Scores API - C# .NET 8\nEnvironment: ${envName}\nBase URL: ${baseUrl}\n\nFlow: 1) Login → 2) Token auto-saved → 3) Get/Record scores.`,
      schema:
        "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    variable: [
      { key: "base_url", value: baseUrl, type: "string" },
      { key: "token", value: "", type: "string" },
    ],
    item: [
      {
        name: "Auth",
        item: authEndpoints.map((ep) => {
          const item = { name: ep.name, request: buildRequest(ep) };
          if (ep.path === "/login") item.event = loginTestScript();
          return item;
        }),
      },
      {
        name: "Scores",
        item: scoreEndpoints.map((ep) => ({
          name: ep.name,
          request: buildRequest(ep),
        })),
      },
      {
        name: "System",
        item: healthEndpoints.map((ep) => ({
          name: ep.name,
          request: buildRequest(ep),
        })),
      },
    ],
  };
}

// ── Read production URL if available ─────

let prodUrl =
  "https://DEPLOY_FIRST.execute-api.us-east-1.amazonaws.com/Prod";
const prodUrlEnv = process.env.PROD_URL;
if (prodUrlEnv) prodUrl = prodUrlEnv;

// ── Generate files ───────────────────────

mkdirSync("postman", { recursive: true });

const localCollection = buildCollection(
  "Local",
  "http://localhost:3002",
  "school-api-dotnet-local"
);
const prodCollection = buildCollection(
  "Production",
  prodUrl,
  "school-api-dotnet-production"
);

writeFileSync(
  "postman/school-api-dotnet-local.postman_collection.json",
  JSON.stringify(localCollection, null, 2)
);
writeFileSync(
  "postman/school-api-dotnet-production.postman_collection.json",
  JSON.stringify(prodCollection, null, 2)
);

console.log(
  `Generated postman/school-api-dotnet-local.postman_collection.json (${endpoints.length} endpoints)`
);
console.log(
  `Generated postman/school-api-dotnet-production.postman_collection.json (${endpoints.length} endpoints)`
);
