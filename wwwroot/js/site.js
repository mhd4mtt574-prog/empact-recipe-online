const menuButton = document.getElementById('menuButton');
const sidebar = document.getElementById('sidebar');
if (menuButton && sidebar) {
  menuButton.addEventListener('click', () => sidebar.classList.toggle('open'));
  document.addEventListener('click', event => {
    if (window.innerWidth <= 900 && sidebar.classList.contains('open') && !sidebar.contains(event.target) && event.target !== menuButton) {
      sidebar.classList.remove('open');
    }
  });
}
