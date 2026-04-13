# ============================================
# school-api-dotnet · Makefile
# ============================================

REGION := us-east-1
STACK  := school-api-dotnet
GH_ORG := Paynau-test

.PHONY: install build deploy db-deploy destroy dev logs-aws help

# ── Setup ───────────────────────────────────

install:
	@cd src && dotnet restore

# ── Local Development ───────────────────────

dev:
	@cd src && dotnet run --urls "http://localhost:3002"

# ── Build & Deploy ──────────────────────────

build:
	@sam build

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
	sam build && \
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
			JwtSecret=school-jwt-secret-prod-2026 \
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

# ── GitHub ──────────────────────────────────

push:
	@if ! gh repo view $(GH_ORG)/school-api-dotnet > /dev/null 2>&1; then \
		gh repo create $(GH_ORG)/school-api-dotnet --public --source=. --push; \
	else \
		git push origin main; \
	fi

# ── Help ────────────────────────────────────

help:
	@echo ""
	@echo "school-api-dotnet commands:"
	@echo ""
	@echo "  make install      Restore NuGet packages"
	@echo "  make dev          Run locally (port 3002)"
	@echo ""
	@echo "  make build        SAM build"
	@echo "  make deploy       Build and deploy to AWS"
	@echo "  make deploy-info  Show deployed API URL"
	@echo "  make destroy      Delete stack"
	@echo ""
	@echo "  make logs-aws     Tail CloudWatch logs"
	@echo "  make push         Push to GitHub"
	@echo ""
