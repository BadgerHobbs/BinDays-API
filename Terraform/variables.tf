# DigitalOcean access token.
variable "do_access_token" {
    type = string
    sensitive = true
}

# ghcr.io username.
variable "ghcr_username" {
    type = string
}

# ghcr.io access token (password).
variable "ghcr_access_token" {
    type = string
    sensitive = true
}

# Docker image.
variable "docker_image" {
    type = string
}
