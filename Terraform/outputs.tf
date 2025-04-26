output "ip_address" {
  value = digitalocean_droplet.bindays_api.ipv4_address
  description = "The public IP address of your BinDays API droplet."
}
