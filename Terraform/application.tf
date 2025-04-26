# Configure the DigitalOcean Provider
provider "digitalocean" {
  token = var.do_access_token
}

# Configure the DigitalOcean SSH key
resource "digitalocean_ssh_key" "bindays_api" {
  name       = "bindays-api-key"
  public_key = file("~/.ssh/id_rsa.pub")
}

# Configure the DigitalOcean Droplet
resource "digitalocean_droplet" "bindays_api" {
  name   = "bindays-api"
  region = "fra1"
  size   = "s-1vcpu-512mb-10gb"
  image  = "debian-12-x64"
  ssh_keys = [digitalocean_ssh_key.bindays_api.id]

  provisioner "remote-exec" {
    connection {
      type        = "ssh"
      user        = "root"
      private_key = file("~/.ssh/id_rsa")
      host        = self.ipv4_address
    }

    inline = [
      "apt update",
      "apt upgrade -y",
      "apt install -y docker.io"
    ]
  }

  depends_on = [
    digitalocean_ssh_key.bindays_api
  ]
}

# A null resource is used here to trigger the Docker update process
# when a specific value (the Docker image tag) changes.
resource "null_resource" "bindays_api" {
  triggers = {
    docker_image_tag = var.docker_image
  }

  provisioner "remote-exec" {
    connection {
      type        = "ssh"
      user        = "root"
      private_key = file("~/.ssh/id_rsa")
      host        = digitalocean_droplet.bindays_api.ipv4_address
    }

    inline = [
      "docker login -u ${var.ghcr_username} -p ${var.ghcr_access_token} ghcr.io/${var.ghcr_username}",
      "docker pull ghcr.io/${var.ghcr_username}/${var.docker_image}",
      "docker rm bindays-api -f",
      "docker run -d --name bindays-api --restart always -p 80:8080 ghcr.io/${var.ghcr_username}/${var.docker_image}"
    ]
  }

  depends_on = [
    digitalocean_droplet.bindays_api
  ]
}