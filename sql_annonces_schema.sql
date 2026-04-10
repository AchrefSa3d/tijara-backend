-- ─── ANNONCES ─────────────────────────────────────────────────────
CREATE TABLE annonces (
  id               INT IDENTITY(1,1) PRIMARY KEY,
  user_id          INT NOT NULL REFERENCES users(id),
  title            NVARCHAR(255) NOT NULL,
  content          NVARCHAR(MAX) NOT NULL,
  image_url        NVARCHAR(MAX) NULL,
  type             NVARCHAR(20)  NOT NULL DEFAULT 'annonce',  -- 'annonce' | 'deal'
  status           NVARCHAR(20)  NOT NULL DEFAULT 'pending',  -- 'pending' | 'approved' | 'rejected'
  rejection_reason NVARCHAR(500) NULL,
  likes_count      INT NOT NULL DEFAULT 0,
  comments_count   INT NOT NULL DEFAULT 0,
  created_at       DATETIME NOT NULL DEFAULT GETDATE(),
  updated_at       DATETIME NOT NULL DEFAULT GETDATE()
);

-- ─── RÉACTIONS (likes) ────────────────────────────────────────────
CREATE TABLE annonce_reactions (
  id          INT IDENTITY(1,1) PRIMARY KEY,
  annonce_id  INT NOT NULL REFERENCES annonces(id) ON DELETE CASCADE,
  user_id     INT NOT NULL REFERENCES users(id),
  created_at  DATETIME NOT NULL DEFAULT GETDATE(),
  CONSTRAINT uq_reaction UNIQUE(annonce_id, user_id)
);

-- ─── COMMENTAIRES ─────────────────────────────────────────────────
CREATE TABLE annonce_comments (
  id          INT IDENTITY(1,1) PRIMARY KEY,
  annonce_id  INT NOT NULL REFERENCES annonces(id) ON DELETE CASCADE,
  user_id     INT NOT NULL REFERENCES users(id),
  content     NVARCHAR(1000) NOT NULL,
  created_at  DATETIME NOT NULL DEFAULT GETDATE()
);

-- ─── APPROBATION PRODUITS ─────────────────────────────────────────
ALTER TABLE products ADD approval_status NVARCHAR(20) NOT NULL DEFAULT 'pending';
UPDATE products SET approval_status = 'approved';   -- les produits existants sont approuvés
