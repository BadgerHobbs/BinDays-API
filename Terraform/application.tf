
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
  region = "lon1"
  size   = "s-1vcpu-512mb-10gb"
  image  = "debian-12-x64"
  ssh_keys = [ digitalocean_ssh_key.bindays_api.id ]

  provisioner "remote-exec" {
    connection {
      type        = "ssh"
      user        = "root"
      private_key = file("~/.ssh/id_rsa.pub")
      host        = self.ipv4_address
    }

    inline = [
      "apt update",
      "apt install -y docker.io",
      "docker login -u ${var.ghcr_username} -p ${var.ghcr_access_token} ghcr.io/${var.ghcr_username}",
      "docker pull ghcr.io/${var.ghcr_username}/${var.docker_image}",
      "docker run -d --restart always -p 80:8080 ghcr.io/${var.ghcr_username}/${var.docker_image}"
    ]
  }

  depends_on = [
    digitalocean_ssh_key.bindays_api
  ]
}
