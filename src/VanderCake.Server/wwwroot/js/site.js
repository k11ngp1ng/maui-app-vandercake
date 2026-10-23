"use strict";
const allocation = document.querySelector("[data-allocation]");
if (allocation) {
    const update = () => {
        let requested = 0, allocated = 0, count = 0, invalid = false;
        const sums = new Map();
        allocation.querySelectorAll("[data-order]").forEach(order => {
            const selected = order.querySelector("[data-select]").checked;
            if (selected) count++;
            order.classList.toggle("selected-order", selected);
            order.querySelectorAll("[data-quantity]").forEach(input => {
                input.setCustomValidity("");
                const value = Number(input.value);
                if (selected) {
                    requested += Number(input.dataset.requested);
                    if (!Number.isInteger(value) || value < 0 || value > Number(input.max)) {
                        input.setCustomValidity("Enter whole units between 0 and the requested quantity."); invalid = true;
                    } else { allocated += value; sums.set(input.dataset.product, (sums.get(input.dataset.product) || 0) + value); }
                }
            });
        });
        const excess = [];
        allocation.querySelectorAll("[data-stock]").forEach(row => {
            const total = sums.get(row.dataset.stock) || 0, remaining = Number(row.dataset.available) - total;
            row.querySelector("[data-allocated]").textContent = String(total);
            row.querySelector("[data-remaining]").textContent = String(remaining);
            row.classList.toggle("stock-exceeded", remaining < 0);
            if (remaining < 0) excess.push(row.cells[0].textContent.trim());
        });
        allocation.querySelector("#allocation-totals").textContent = `${count} selected order(s) · Requested ${requested} · Allocated ${allocated} · Unfulfilled ${requested - allocated} units`;
        allocation.querySelector("#allocation-error").textContent = excess.length ? `Allocation exceeds eligible stock: ${excess.join(", ")}. Reduce quantities.` : invalid ? "Correct the highlighted quantities before confirming." : "";
        allocation.querySelector("[data-confirm]").disabled = !count || invalid || excess.length > 0;
    };
    allocation.addEventListener("input", update); update();
}
document.querySelectorAll("form[method='post']").forEach(form => {
    form.addEventListener("submit", () => {
        if (!form.checkValidity()) return;
        document.querySelector("#busy-status").hidden = false;
        form.setAttribute("aria-busy", "true");
        form.querySelectorAll("button[type='submit'], button:not([type])").forEach(button => { button.disabled = true; });
    });
});
window.addEventListener("pageshow", () => { document.querySelector("#busy-status").hidden = true; document.querySelectorAll("form[aria-busy]").forEach(form => { form.removeAttribute("aria-busy"); form.querySelectorAll("button").forEach(button => button.disabled = false); }); if (allocation) allocation.dispatchEvent(new Event("input")); });
