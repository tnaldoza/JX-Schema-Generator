import { RuleConfigSeverity, type UserConfig } from "@commitlint/types";

const config: UserConfig = {
	extends: ["@commitlint/config-conventional"],
	// ! Dependabot writes lowercase "bump" subjects and deps-dev scopes
	ignores: [(message) => /^(chore|ci)\(deps(-dev)?\): bump /i.test(message)],
	rules: {
		"type-enum": [RuleConfigSeverity.Error, "always", ["feat", "fix", "refactor", "perf", "test", "docs", "chore", "ci", "revert", "security"]],
		"scope-enum": [
			RuleConfigSeverity.Warning,
			"always",
			["repo", "cli", "schema", "expand", "generate", "emit", "fetch", "scripts", "tests", "build", "deps", "ci", "docs", "release"],
		],
		"subject-case": [RuleConfigSeverity.Error, "always", "sentence-case"],
		"header-max-length": [RuleConfigSeverity.Error, "always", 100],
		"body-max-line-length": [RuleConfigSeverity.Error, "always", 120],
	},
};

export default config;
