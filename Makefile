# ============================================
# school-api · Makefile
# ============================================

REGION := us-east-1
STACK  := school-api-dotnet
GH_ORG := Paynau-test

.PHONY: install build deploy destroy dev stop logs dev-native logs-aws postman postman-prod push help

# ── Setup ───────────────────────────────────

install:
	@docker compose build api

# ── Local Development ───────────────────────

dev:
	@docker compose down 2>/dev/null || true
	@docker compose up -d --build api
	@echo "API running at http://localhost:3002"
	@echo "  make logs → ver logs"
	@echo "  make stop → detener"

stop:
	@docker compose down
	@echo "Stopped."

logs:
	@docker compose logs -f api

dev-native:
	@cd src && dotnet run --urls "http://localhost:3002"

# ── Build & Deploy ──────────────────────────

build:
	@sam build --use-container

deploy:
	@echo "Reading infra outputs from AWS..."
	@VPC_ID=$$(aws cloudformation describe-stacks --stack-name SchoolNetwork --region $(REGION) \
		--query 'Stacks[0].Outputs[?OutputKey==`VpcId`].OutputValue' --output text) && \
	SUBNETS=$$(aws ec2 describe-subnets --region $(REGION) \
		--filters "Name=vpc-id,Values=$$VPC_ID" "Name=tag:aws-cdk:subnet-type,Values=Private" \
		--query 'Subnets[].SubnetId' --output text | tr '\t' ',') && \
	DB_HOST=$$(aws cloudformation describe-stacks --stack-name SchoolDatabase --region $(REGION) \
		--query 'Stacks[0].Outputs[?OutputKey==`DbEndpoint`].OutputValue' --output text) && \
	DB_SECRET=$$(aws secretsmanager get-secret-value --secret-id school-db-credentials --region $(REGION) \
		--query SecretString --output text) && \
	DB_USER=$$(echo "$$DB_SECRET" | python3 -c "import sys,json; print(json.load(sys.stdin)['username'])") && \
	DB_PASS=$$(echo "$$DB_SECRET" | python3 -c "import sys,json; print(json.load(sys.stdin)['password'])") && \
	echo "VPC: $$VPC_ID" && \
	echo "Subnets: $$SUBNETS" && \
	echo "DB Host: $$DB_HOST" && \
	sam build --use-container && \
	sam deploy --stack-name $(STACK) \
		--region $(REGION) \
		--capabilities CAPABILITY_IAM \
		--resolve-s3 \
		--no-confirm-changeset \
		--tags project=school environment=dev owner=isaac \
		--parameter-overrides \
			VpcId=$$VPC_ID \
			SubnetIds=$$SUBNETS \
			DbHost=$$DB_HOST \
			DbUser=$$DB_USER \
			DbPassword=$$DB_PASS \
			DbName=school_db \
			JwtSecret=school-jwt-secret-prod-2026-dotnet \
	|| echo "Stack already up to date."

deploy-info:
	@API_URL=$$(aws cloudformation describe-stacks \
		--stack-name $(STACK) --region $(REGION) \
		--query 'Stacks[0].Outputs[?OutputKey==`ApiUrl`].OutputValue' \
		--output text 2>/dev/null) && \
	echo "API URL: $$API_URL"

destroy:
	@echo "Destroying $(STACK) stack..."
	@sam delete --stack-name $(STACK) --no-prompts
	@echo "Done."

# ── Logs ────────────────────────────────────

logs-aws:
	@sam logs --stack-name $(STACK) --tail

# ── Postman ─────────────────────────────

postman:
	@node scripts/generate-postman.js
	@echo ""
	@echo "Import into Postman:"
	@echo "  postman/school-api-local.postman_collection.json"
	@echo "  postman/school-api-production.postman_collection.json"

postman-prod:
	@API_URL=$$(aws cloudformation describe-stacks \
		--stack-name $(STACK) --region $(REGION) \
		--query 'Stacks[0].Outputs[?OutputKey==`ApiUrl`].OutputValue' \
		--output text 2>/dev/null) && \
	PROD_URL=$$API_URL node scripts/generate-postman.js && \
	echo "" && \
	echo "Production URL: $$API_URL" && \
	echo "Import: postman/school-api-production.postman_collection.json"

# ── GitHub ──────────────────────────────────

push:
	@if ! gh repo view $(GH_ORG)/school-api > /dev/null 2>&1; then \
		gh repo create $(GH_ORG)/school-api --public --source=. --push; \
	else \
		git push origin main; \
	fi

# ── Help ────────────────────────────────────

help:
	@echo ""
	@echo "school-api commands:"
	@echo ""
	@echo "  make install      Build Docker image"
	@echo "  make dev          Run in Docker (port 3002)"
	@echo "  make stop         Stop container"
	@echo "  make logs         Tail container logs"
	@echo "  make dev-native   Run without Docker (needs .NET SDK)"
	@echo ""
	@echo "  make build        SAM build"
	@echo "  make deploy       Build and deploy to AWS"
	@echo "  make deploy-info  Show deployed API URL"
	@echo "  make destroy      Delete stack"
	@echo ""
	@echo "  make logs-aws     Tail CloudWatch logs"
	@echo "  make push         Push to GitHub"
	@echo ""
	@echo "  make postman      Generate Postman collections (local + prod placeholder)"
	@echo "  make postman-prod Generate Postman collections with real prod URL from AWS"
	@echo ""
