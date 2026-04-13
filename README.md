# school-api-dotnet

REST API for school scores management built with C# .NET 8, deployed as AWS Lambda via SAM.

## Endpoints

| Method | Path              | Auth | Description                         |
|--------|-------------------|------|-------------------------------------|
| POST   | /login            | No   | JWT authentication                  |
| GET    | /students/search  | Yes  | Search students by ID or name       |
| GET    | /grades           | Yes  | List all grades                     |
| GET    | /scores           | Yes  | Get scores by student/grade/month   |
| POST   | /scores           | Yes  | Record or update a score (upsert)   |
| GET    | /health           | No   | Health check                        |

## GET /scores

Query parameters: `studentId`, `gradeId`, `year`, `month`

Returns all subjects for the grade with their score (null if not recorded).

## POST /scores

Body: `{ studentId, subjectId, gradeId, year, month, score }`

Score range: 0.00 - 10.00. Performs upsert (create or update).

## Setup

```bash
# Install dependencies
make install

# Run locally (port 3002)
make dev

# Deploy to AWS
make deploy
```

## Architecture

- **Runtime**: .NET 8 (C#) on AWS Lambda
- **Database**: MySQL 8 (shared RDS instance via stored procedures)
- **Auth**: JWT (same tokens as school-api-node)
- **Deploy**: SAM (CloudFormation)

## Stored Procedures Used

- `sp_get_user_by_email` — Get user by email for login (BCrypt verification in app)
- `sp_get_scores` — Get all subjects for a grade with scores (LEFT JOIN)
- `sp_record_score` — Upsert a score record
