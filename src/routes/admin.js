const express = require('express');
const router  = express.Router();
const { authenticate, authorize } = require('../middleware/auth');
const {
  getPendingVendors, getAllVendors,
  approveVendor, rejectVendor,
  getAllUsers, getStats,
  getAllOrders, updateOrderStatus,
  getAllProducts,
  getAllAnnonces, approveAnnonce, rejectAnnonce,
  getPendingProducts, approveProduct, rejectProduct,
} = require('../controllers/adminController');

// Toutes les routes admin nécessitent d'être authentifié + rôle admin
router.use(authenticate, authorize('admin'));

router.get('/stats',                      getStats);
router.get('/users',                      getAllUsers);
router.get('/vendors',                    getAllVendors);
router.get('/vendors/pending',            getPendingVendors);
router.patch('/vendors/:id/approve',      approveVendor);
router.patch('/vendors/:id/reject',       rejectVendor);

// Commandes
router.get('/orders',                     getAllOrders);
router.patch('/orders/:id/status',        updateOrderStatus);

// Tous les produits
router.get('/all-products',               getAllProducts);

// Modération annonces
router.get('/annonces',                   getAllAnnonces);
router.patch('/annonces/:id/approve',     approveAnnonce);
router.patch('/annonces/:id/reject',      rejectAnnonce);

// Modération produits
router.get('/products/pending',           getPendingProducts);
router.patch('/products/:id/approve',     approveProduct);
router.patch('/products/:id/reject',      rejectProduct);

module.exports = router;
