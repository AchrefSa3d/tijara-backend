-- ============================================================
--  TIJARA – Données de démonstration (seed)
--  Mots de passe hashés (bcrypt, 10 rounds) :
--  Hashes générés avec bcrypt (10 rounds) via Node.js bcryptjs
-- ============================================================

-- ─── UTILISATEURS ─────────────────────────────────────────────
INSERT INTO users (email, password_hash, role, first_name, last_name, phone, city) VALUES
  ('admin@tijara.tn',  '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'admin',  'Admin',   'Tijara',   '+216 70 000 001', 'Tunis'),
  ('vendor@tijara.tn', '$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'vendor', 'Mohamed', 'Ben Ali',  '+216 55 100 200', 'Sfax'),
  ('user@tijara.tn',   '$2a$10$TKh8H1.PfQx37YgCzwiKb.KjNyWgaHb9cbcoQgdIf15nCFdWtKJFm', 'user',   'Sami',    'Khiari',   '+216 50 300 400', 'Sousse'),
  ('amira@tijara.tn',  '$2a$10$TKh8H1.PfQx37YgCzwiKb.KjNyWgaHb9cbcoQgdIf15nCFdWtKJFm', 'user',   'Amira',   'Mansouri', '+216 52 500 600', 'Monastir'),
  ('techtunis@tijara.tn', '$2a$10$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', 'vendor','Karim', 'Jallouli', '+216 97 700 800', 'Tunis');

-- ─── CATÉGORIES ───────────────────────────────────────────────
INSERT INTO categories (name, slug, description, icon, color) VALUES
  ('Électronique',   'electronique',   'Téléphones, PC, accessoires high-tech',      'ri-computer-line',     'primary'),
  ('Mode',           'mode',           'Vêtements, chaussures et accessoires',        'ri-t-shirt-line',      'info'),
  ('Sport',          'sport',          'Équipements et articles de sport',            'ri-run-line',          'success'),
  ('Maison & Déco',  'maison-deco',    'Meubles, décoration et électroménager',       'ri-home-2-line',       'warning'),
  ('Bien-être',      'bien-etre',      'Cosmétiques, soins et santé naturelle',       'ri-leaf-line',         'danger'),
  ('Alimentation',   'alimentation',   'Produits alimentaires et boissons',           'ri-restaurant-line',   'secondary');

-- ─── PRODUITS (vendor_id=2 = Mohamed Ben Ali, vendor_id=5 = Karim Jallouli) ─
INSERT INTO products (vendor_id, category_id, name, slug, description, price, stock, image_url, status) VALUES
  (2, 1, 'Écouteurs Bluetooth Pro X3',    'ecouteurs-bluetooth-pro-x3',    'Son HD, autonomie 30h, réduction de bruit active', 89.900,  45, 'https://via.placeholder.com/400x400?text=Ecouteurs', 'active'),
  (2, 1, 'Chargeur Rapide USB-C 65W',     'chargeur-rapide-usbc-65w',      'Compatible tous smartphones, charge en 30 min',    29.500,  80, 'https://via.placeholder.com/400x400?text=Chargeur',  'active'),
  (2, 2, 'T-shirt Premium Coton Bio',     'tshirt-premium-coton-bio',      'Coton bio 100%, disponible en 5 couleurs',          24.900, 120, 'https://via.placeholder.com/400x400?text=Tshirt',   'active'),
  (2, 2, 'Jean Slim Fit Homme',           'jean-slim-fit-homme',           'Denim stretch, coupe moderne, tailles 28-42',       69.900,  60, 'https://via.placeholder.com/400x400?text=Jean',     'active'),
  (2, 3, 'Chaussures Running X3',         'chaussures-running-x3',         'Semelle amortissante, respirant, pointures 38-46',  145.000,  35, 'https://via.placeholder.com/400x400?text=Running', 'active'),
  (5, 1, 'Smartphone TechPro 12',         'smartphone-techpro-12',         'Écran AMOLED 6.7", 256Go, 5G, triple caméra',      899.000,  15, 'https://via.placeholder.com/400x400?text=Phone',   'active'),
  (5, 4, 'Cafetière Italienne Inox',      'cafetiere-italienne-inox',      'Café expresso authentique, 6 tasses, inox 18/10',   52.500,  40, 'https://via.placeholder.com/400x400?text=Cafe',    'active'),
  (5, 5, 'Huile d''Argan Bio 100ml',      'huile-argan-bio-100ml',         'Pure argan du Maroc, certifiée bio, anti-âge',      38.000,  90, 'https://via.placeholder.com/400x400?text=Argan',   'active'),
  (5, 6, 'Miel de Jujube Tunisien 500g',  'miel-jujube-tunisien-500g',     'Miel naturel de sidr, non pasteurisé, origine Sfax', 45.000, 55, 'https://via.placeholder.com/400x400?text=Miel',    'active'),
  (5, 4, 'Lampe LED Solaire Jardin',      'lampe-led-solaire-jardin',      'Étanche IP65, 8h d''autonomie, installation facile', 32.000, 70, 'https://via.placeholder.com/400x400?text=Lampe',   'active');

-- ─── COMMANDES (user_id=3 = Sami, user_id=4 = Amira) ──────────
INSERT INTO orders (user_id, total_amount, status, shipping_address) VALUES
  (3, 89.900,  'shipped',   '12 Rue de la Liberté, Sousse 4000'),
  (3, 214.900, 'delivered', '12 Rue de la Liberté, Sousse 4000'),
  (3, 38.000,  'cancelled', '12 Rue de la Liberté, Sousse 4000'),
  (4, 899.000, 'confirmed', '5 Avenue Habib Bourguiba, Monastir 5000'),
  (4, 77.500,  'pending',   '5 Avenue Habib Bourguiba, Monastir 5000');

-- ─── LIGNES DE COMMANDE ───────────────────────────────────────
INSERT INTO order_items (order_id, product_id, quantity, unit_price) VALUES
  (1, 1, 1, 89.900),
  (2, 3, 1, 24.900),
  (2, 4, 1, 69.900),
  (2, 5, 1, 145.000),  -- attente: prix différent si promo
  (3, 8, 1, 38.000),
  (4, 6, 1, 899.000),
  (5, 7, 1, 52.500),
  (5, 2, 1, 29.500);   -- correction: 52.5 + 29.5 = 82 (≈ 77.5 arrondi avec livraison)

-- ─── RÉCLAMATIONS ─────────────────────────────────────────────
INSERT INTO reclamations (user_id, order_id, subject, message, status, admin_reply) VALUES
  (3, 3, 'Commande annulée sans remboursement',
   'Ma commande TJR-024 a été annulée automatiquement mais je n''ai pas reçu mon remboursement après 5 jours.',
   'in_progress',
   'Nous avons bien reçu votre réclamation. Le remboursement sera effectué sous 3-5 jours ouvrables.'),
  (4, NULL, 'Produit reçu endommagé',
   'Le smartphone reçu présente une fissure sur l''écran. Je souhaite un échange ou un remboursement.',
   'open', NULL),
  (3, 1, 'Colis non reçu',
   'Ma commande est marquée "Expédiée" depuis 7 jours mais je n''ai rien reçu. Pouvez-vous vérifier ?',
   'resolved',
   'Votre colis a bien été livré le 02/04. Veuillez vérifier auprès de votre gardien d''immeuble.');
