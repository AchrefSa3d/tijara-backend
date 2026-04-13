const express = require('express');
const router  = express.Router();
const { authenticate } = require('../middleware/auth');
const {
  getConversations,
  getConversationMessages,
  sendMessage,
  startConversation,
} = require('../controllers/messageController');

router.get('/conversations',          authenticate, getConversations);
router.get('/conversations/:id',      authenticate, getConversationMessages);
router.post('/conversations/:id',     authenticate, sendMessage);
router.post('/start',                 authenticate, startConversation);

module.exports = router;
